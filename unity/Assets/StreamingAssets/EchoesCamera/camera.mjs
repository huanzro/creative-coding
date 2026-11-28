import { handOpenness } from './core.js';
import { HandFrameClock, IndexTipTracker, handSampleInterval, loadHandDetector, withIndexTip } from './index-tip.mjs';

const video = document.getElementById('preview');
const previewFrame = document.getElementById('preview-frame');
const status = document.getElementById('status');
const button = document.getElementById('camera');
const previewButton = document.getElementById('popout');
const indexTracker = new IndexTipTracker();
const confirmPoseAfter = 38;
const handLostAfter = 320;
const openThreshold = 1.58, closedThreshold = 1.38;
const receivedPose = {
  open: 'Unity 已收到：张手',
  closed: 'Unity 已收到：握拳',
  missing: 'Unity 已连接。把一只手放进画面。'
};
let stream, detector, frameReader, timer, queuedState, captureRetry, bridgeRetry;
let worker, pendingFrame, frameInWorker = false, readingPreview = false;
let previewWindow, previewStatus;
let generation = 0, sending = false, connected = true;
let wantsCamera = false, hadFrames = false, captureAttempts = 0, bridgeLostAt = -1;
let lastFrameAt = 0, workerSentAt = 0;
let videoTime = -1, lastLandmarksAt = -Infinity;
let pose = 'missing', candidate = null, candidateAt = 0;

function setStatus(text) {
  if (status.textContent === text) return;
  status.textContent = text;
  if (previewStatus) previewStatus.textContent = text;
}

function placeTip(frame, feed, tip) {
  const marker = frame.querySelector('.index-tip');
  marker.hidden = !tip || feed.videoWidth === 0 || frame.clientHeight === 0;
  if (marker.hidden) return;
  const scale = Math.min(frame.clientWidth / feed.videoWidth, frame.clientHeight / feed.videoHeight);
  marker.style.left = frame.clientWidth * .5 + (tip.x - .5) * feed.videoWidth * scale + 'px';
  marker.style.top = frame.clientHeight * .5 + (tip.y - .5) * feed.videoHeight * scale + 'px';
}

async function togglePreview() {
  if (!stream) return;
  if (previewWindow && !previewWindow.closed) {
    previewWindow.close();
    return;
  }
  previewButton.disabled = true;
  const current = generation;
  let popup;
  try {
    if (window.documentPictureInPicture) {
      try {
        popup = await window.documentPictureInPicture.requestWindow({ width: 352, height: 308 });
      } catch { /* Fall back to a separate browser window. */ }
    }
    if (!popup) popup = window.open('', '', 'popup=yes,width=352,height=308');
    if (!popup) {
      previewButton.textContent = '允许弹窗后重试';
      return;
    }
    if (generation !== current) {
      popup.close();
      return;
    }
    const doc = popup.document;
    doc.title = '光的回响 · 手势定位';
    doc.documentElement.lang = 'zh-CN';
    doc.head.append(document.getElementById('camera-style').cloneNode(true));
    doc.body.className = 'floating-preview';
    const frame = previewFrame.cloneNode(true);
    const feed = frame.querySelector('video');
    feed.muted = true;
    feed.srcObject = stream;
    const label = doc.createElement('p');
    label.className = 'preview-status';
    label.textContent = status.textContent;
    doc.body.append(frame, label);
    void feed.play().catch(() => {});
    previewWindow = popup;
    previewStatus = label;
    previewButton.textContent = '关闭独立预览';
    popup.addEventListener('pagehide', () => {
      if (previewWindow !== popup) return;
      previewWindow = previewStatus = null;
      previewButton.textContent = '打开独立预览';
    }, { once: true });
  } catch {
    popup?.close();
    previewButton.textContent = '重试打开独立预览';
  } finally {
    previewButton.disabled = !stream;
  }
}

function report(state) {
  queuedState = { state, at: performance.now(), generation };
  void flushReport();
}

async function flushReport() {
  if (sending || !connected || bridgeRetry != null) return;
  sending = true;
  try {
    while (queuedState) {
      const packet = queuedState;
      queuedState = null;
      if (packet.generation !== generation) continue;
      const posePacket = packet.state === 'open' || packet.state === 'closed' || packet.state.includes('/');
      const state = posePacket && performance.now() - packet.at > 300 ? 'missing' : packet.state;
      const abort = new AbortController();
      const deadline = setTimeout(() => abort.abort(), 1200);
      try {
        const response = await fetch('./state/' + state,
          { method: 'POST', cache: 'no-store', signal: abort.signal });
        if (response.status === 404 || response.status === 410) { bridgeClosed(); break; }
        if (!response.ok) throw new Error('Unity connection interrupted');
        bridgeLostAt = -1;
        const label = state.includes('/') ? 'Unity 已收到：食指位置' : receivedPose[state];
        if (packet.generation === generation && label) setStatus(label);
      } catch {
        if (!connected) break;
        if (packet.generation !== generation) continue;
        const now = performance.now();
        if (bridgeLostAt < 0) bridgeLostAt = now;
        if (now - bridgeLostAt >= 12000) { bridgeClosed(); break; }
        // A retry without a new frame probes the bridge without replaying a pose.
        queuedState ??= { ...packet, state: posePacket ? 'missing' : packet.state, at: now };
        setStatus('正在恢复与 Unity 的连接…');
        bridgeRetry = setTimeout(() => { bridgeRetry = null; void flushReport(); }, 300);
        break;
      } finally {
        clearTimeout(deadline);
      }
    }
  } finally {
    sending = false;
  }
}

function bridgeClosed() {
  connected = false;
  queuedState = null;
  stop('Unity 识别会话已结束。请从 Unity 重新打开识别页。');
  button.disabled = true;
}

function releaseCamera(keepPreview = false) {
  generation++;
  clearInterval(timer);
  void frameReader?.cancel().catch(() => {});
  frameReader = null;
  clearWorker();
  readingPreview = false;
  stream?.getTracks().forEach(track => track.stop());
  stream = null;
  video.srcObject = null;
  if (keepPreview) {
    const feed = previewWindow?.document.querySelector('video');
    if (feed) feed.srcObject = null;
  } else {
    previewWindow?.close();
    previewWindow = previewStatus = null;
    previewButton.textContent = '打开独立预览';
  }
  previewButton.disabled = true;
  pose = 'missing';
  candidate = null;
  lastLandmarksAt = -Infinity;
  indexTracker.reset();
  placeTip(previewFrame, video, null);
}

function stop(message, state = 'off') {
  wantsCamera = false;
  clearTimeout(captureRetry);
  captureRetry = null;
  releaseCamera();
  setStatus(message);
  button.textContent = '开启摄像头';
  button.disabled = !connected;
  if (connected) report(state);
}

function retryCapture(current, message, discardDetector = false) {
  if (current !== generation || !wantsCamera || !connected) return;
  releaseCamera(true);
  if (discardDetector) {
    detector?.close?.();
    detector = null;
  }
  setStatus(message);
  report('recovering');
  button.textContent = '关闭摄像头';
  button.disabled = false;
  const wait = Math.min(4000, 450 * 2 ** Math.min(captureAttempts++, 4));
  captureRetry = setTimeout(() => { captureRetry = null; void start(); }, wait);
}

function clearWorker() {
  worker?.terminate();
  pendingFrame?.frame.close();
  worker = pendingFrame = null;
  frameInWorker = false;
}

function recoverDetector(current) {
  if (generation !== current || !worker) return;
  clearWorker();
  if (detector) return;
  void loadHandDetector().then(next => {
    if (generation === current) detector = next;
    else next.close?.();
  }).catch(() => {
    retryCapture(current, '正在重启手势识别…', true);
  });
}

async function useWorker(current) {
  if (typeof Worker !== 'function' || typeof OffscreenCanvas !== 'function') return false;
  let thread;
  try {
    thread = new Worker(new URL('./hand-worker.js', import.meta.url));
    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('Hand worker did not start')), 7500);
      thread.onmessage = ({ data }) => {
        clearTimeout(timeout);
        if (data.ready) resolve();
        else reject(new Error('Hand worker is unavailable'));
      };
      thread.onerror = event => {
        event.preventDefault();
        clearTimeout(timeout);
        reject(new Error('Hand worker could not load'));
      };
    });
    if (generation !== current) { thread.terminate(); return false; }
    worker = thread;
    thread.onerror = event => { event.preventDefault(); recoverDetector(current); };
    thread.onmessage = ({ data }) => {
      if (generation !== current || worker !== thread) return;
      if (data.failed) { recoverDetector(current); return; }
      frameInWorker = false;
      receiveLandmarks(data.points, data.aspect, data.at);
      submitLatest();
    };
    return true;
  } catch {
    thread?.terminate();
    return false;
  }
}

function submitLatest() {
  if (!worker || frameInWorker || !pendingFrame) return;
  const next = pendingFrame;
  pendingFrame = null;
  frameInWorker = true;
  workerSentAt = performance.now();
  try {
    worker.postMessage(next, [next.frame]);
  } catch {
    next.frame.close();
    recoverDetector(generation);
  }
}

function keepLatest(frame, at) {
  // A slow inference consumes the most recent frame next, without replaying old hand motions.
  pendingFrame?.frame.close();
  pendingFrame = { frame, at };
  submitLatest();
}

function receiveLandmarks(points, aspect, now) {
  hadFrames = true;
  captureAttempts = 0;
  const tip = indexTracker.update(points, aspect, now);
  const score = handOpenness(points?.map(point => ({ x: point.x * aspect, y: point.y, z: point.z * aspect })));
  if (now - lastLandmarksAt >= handLostAfter) {
    pose = 'missing';
    candidate = null;
  }
  if (score !== null) lastLandmarksAt = now;
  const observed = score === null ? null : score >= openThreshold ? 'open' : score <= closedThreshold ? 'closed' : null;
  if (observed !== candidate) {
    candidate = observed;
    candidateAt = now;
  } else if (candidate && now - candidateAt >= confirmPoseAfter) {
    pose = candidate;
  }
  report(withIndexTip(pose, tip));
  placeTip(previewFrame, video, tip);
  const floating = previewWindow?.document.querySelector('.preview-frame');
  if (floating) placeTip(floating, floating.querySelector('video'), tip);
}

async function trackFrames(reader, current) {
  const clock = new HandFrameClock();
  try {
    while (generation === current) {
      const { value: frame, done } = await reader.read();
      if (done) break;
      let transferred = false;
      try {
        const now = performance.now();
        if (generation !== current) continue;
        lastFrameAt = now;
        checkCapture(current);
        if (!clock.take(frame.timestamp == null ? now : frame.timestamp / 1000)) continue;
        if (worker) {
          keepLatest(frame, now);
          transferred = true;
        } else if (detector) {
          const bitmap = await createImageBitmap(frame);
          try {
            if (generation === current) receiveLandmarks(detector.detectForVideo(bitmap, now).landmarks[0],
              bitmap.width / bitmap.height, now);
          } finally { bitmap.close(); }
        }
      } finally { if (!transferred) frame.close(); }
    }
    retryCapture(current, '摄像头画面中断，正在重新连接…');
  } catch {
    retryCapture(current, '正在恢复摄像头和手势识别…', true);
  } finally {
    reader.releaseLock();
  }
}

function trackPreview() {
  if (!stream || frameReader) return;
  const now = performance.now();
  try {
    if (video.readyState >= 2 && videoTime !== video.currentTime && !readingPreview) {
      videoTime = video.currentTime;
      lastFrameAt = now;
      if (worker) {
        readingPreview = true;
        const current = generation;
        void createImageBitmap(video).then(bitmap => {
          if (generation === current && worker) keepLatest(bitmap, now);
          else bitmap.close();
        }).catch(() => {}).finally(() => {
          if (generation === current) readingPreview = false;
        });
      } else if (detector) {
        receiveLandmarks(detector.detectForVideo(video, now).landmarks[0], video.videoWidth / video.videoHeight, now);
      }
    }
  } catch {
    retryCapture(generation, '正在恢复手势识别…', true);
  }
}

function checkCapture(current) {
  if (current !== generation || !stream) return;
  const now = performance.now();
  if (worker && frameInWorker && now - workerSentAt > 1600) recoverDetector(current);
  if ((frameReader || !document.hidden) && now - lastFrameAt > 3500)
    retryCapture(current, '摄像头画面停滞，正在重新连接…');
}

async function start() {
  if (!wantsCamera || !connected) return;
  const current = ++generation;
  button.textContent = '关闭摄像头';
  button.disabled = false;
  setStatus('等待浏览器的摄像头权限…');
  report(hadFrames ? 'recovering' : 'loading');
  try {
    const next = await navigator.mediaDevices.getUserMedia({
      video: { width: { ideal: 640 }, height: { ideal: 480 }, frameRate: { ideal: 30 }, facingMode: 'user' }, audio: false
    });
    if (generation !== current) { next.getTracks().forEach(track => track.stop()); return; }
    stream = next;
    const track = stream.getVideoTracks()[0];
    track.addEventListener('ended', () => {
      retryCapture(current, '摄像头已断开，正在重新连接…');
    });
    video.srcObject = stream;
    void video.play().catch(() => {});
    const feed = previewWindow?.document.querySelector('video');
    if (feed) {
      feed.srcObject = stream;
      void feed.play().catch(() => {});
    }
    previewButton.disabled = false;
    setStatus('载入手势识别模型…');
    if (!await useWorker(current) && generation === current && !detector) {
      const nextDetector = await loadHandDetector();
      if (generation === current) detector = nextDetector;
      else nextDetector.close?.();
    }
    if (generation !== current) return;
    setStatus('等待摄像头画面…');
    if (typeof MediaStreamTrackProcessor === 'function') {
      // Read the camera directly so a hidden preview tab cannot slow gesture sampling.
      try { frameReader = new MediaStreamTrackProcessor({ track, maxBufferSize: 1 }).readable.getReader(); }
      catch { frameReader = null; }
    }
    lastFrameAt = performance.now();
    videoTime = -1;
    timer = setInterval(() => { trackPreview(); checkCapture(current); }, frameReader ? 250 : handSampleInterval);
    if (frameReader) void trackFrames(frameReader, current);
    button.textContent = '关闭摄像头';
  } catch (error) {
    if (generation !== current) return;
    const failure = {
      NotAllowedError: ['浏览器未获得权限。允许摄像头后点下方按钮重试。', 'denied'],
      NotFoundError: ['系统未找到摄像头。接好设备后重试。', 'not-found'],
      NotReadableError: ['摄像头不可读。关闭可能占用它的程序后重试。', 'busy']
    }[error.name] || ['手势识别模型无法启动。请重开识别页。', 'model-error'];
    if (hadFrames && error.name !== 'NotAllowedError') retryCapture(current, '正在重新连接摄像头…', failure[1] === 'model-error');
    else stop(...failure);
  } finally {
    button.disabled = !connected;
  }
}

function enableCamera() {
  wantsCamera = true;
  hadFrames = false;
  captureAttempts = 0;
  void start();
}

button.addEventListener('click', () => wantsCamera ? stop('摄像头已关闭。') : enableCamera());
previewButton.addEventListener('click', () => void togglePreview());
video.addEventListener('timeupdate', trackPreview);
document.addEventListener('visibilitychange', () => {
  if (!document.hidden && stream) {
    void video.play().catch(() => {});
    trackPreview();
    checkCapture(generation);
  }
});
window.addEventListener('pagehide', () => {
  wantsCamera = false;
  clearTimeout(captureRetry);
  clearTimeout(bridgeRetry);
  releaseCamera();
  if (connected) void fetch('./state/off', { method: 'POST', keepalive: true }).catch(() => {});
  connected = false;
});
enableCamera();
