import { GestureGate, handOpenness } from './core.js';

const video = document.getElementById('preview');
const status = document.getElementById('status');
const button = document.getElementById('camera');
const gesture = new GestureGate();
const frameInterval = 85;
const handLostAfter = 320;
const receivedPose = {
  open: 'Unity 已收到：张手',
  closed: 'Unity 已收到：握拳',
  missing: 'Unity 已连接。把一只手放进画面，先张开手掌。'
};
let stream, detector, frameReader, timer, queuedState;
let generation = 0, sending = false, connected = true;
let videoTime = -1, lastLandmarksAt = -Infinity;

async function report(state) {
  queuedState = state;
  if (sending || !connected) return;
  sending = true;
  try {
    while (queuedState) {
      const next = queuedState;
      const current = generation;
      queuedState = null;
      const response = await fetch('./state/' + next, { method: 'POST', cache: 'no-store' });
      if (!response.ok) throw new Error('Unity connection lost');
      const label = receivedPose[next];
      if (generation === current && label && status.textContent !== label) status.textContent = label;
    }
  } catch {
    connected = false;
    stop('Unity 已关闭连接。请回到 Unity 重新开启摄像头。');
    button.disabled = true;
  } finally {
    sending = false;
  }
}

function releaseCamera() {
  generation++;
  clearInterval(timer);
  void frameReader?.cancel().catch(() => {});
  frameReader = null;
  stream?.getTracks().forEach(track => track.stop());
  stream = null;
  video.srcObject = null;
  gesture.reset();
  lastLandmarksAt = -Infinity;
}

function stop(message, state = 'off') {
  releaseCamera();
  status.textContent = message;
  button.textContent = '开启摄像头';
  void report(state);
}

async function loadDetector() {
  const { FilesetResolver, HandLandmarker } = await import('./vendor/vision_bundle.mjs');
  const vendor = new URL('./vendor/', import.meta.url);
  const files = await FilesetResolver.forVisionTasks(new URL('wasm', vendor).href);
  return HandLandmarker.createFromOptions(files, {
    baseOptions: { modelAssetPath: new URL('hand_landmarker.task', vendor).href, delegate: 'CPU' },
    runningMode: 'VIDEO', numHands: 1, minHandDetectionConfidence: .65,
    minHandPresenceConfidence: .65, minTrackingConfidence: .6
  });
}

function identify(source, aspect, now) {
  const points = source ? detector.detectForVideo(source, now).landmarks[0] : null;
  const score = handOpenness(points?.map(point => ({ x: point.x * aspect, y: point.y, z: point.z * aspect })));
  if (now - lastLandmarksAt >= handLostAfter) gesture.reset();
  if (score !== null) {
    lastLandmarksAt = now;
    gesture.update(score, now);
  }
  void report(gesture.state === 'unknown' ? 'missing' : gesture.state);
}

async function trackFrames(reader, current) {
  let nextFrameAt = 0;
  try {
    while (generation === current) {
      const { value: frame, done } = await reader.read();
      if (done) break;
      try {
        const now = performance.now();
        if (generation !== current || now < nextFrameAt) continue;
        nextFrameAt = now + frameInterval;
        const bitmap = await createImageBitmap(frame);
        try {
          if (generation === current) identify(bitmap, bitmap.width / bitmap.height, now);
        } finally { bitmap.close(); }
      } finally { frame.close(); }
    }
    if (generation === current) stop('摄像头画面已断开，请重新开启。');
  } catch {
    if (generation === current) stop('手势识别中断，请重新开启摄像头。', 'model-error');
  } finally {
    reader.releaseLock();
  }
}

function trackPreview() {
  const now = performance.now();
  try {
    if (video.readyState >= 2 && videoTime !== video.currentTime) {
      videoTime = video.currentTime;
      identify(video, video.videoWidth / video.videoHeight, now);
    } else if (videoTime >= 0 && now - lastLandmarksAt >= handLostAfter) {
      identify(null, 1, now);
    }
  } catch {
    stop('手势识别中断，请重新开启摄像头。', 'model-error');
  }
}

async function start() {
  const current = ++generation;
  button.disabled = true;
  status.textContent = '等待浏览器的摄像头权限…';
  void report('loading');
  try {
    const next = await navigator.mediaDevices.getUserMedia({
      video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' }, audio: false
    });
    if (generation !== current) { next.getTracks().forEach(track => track.stop()); return; }
    stream = next;
    const track = stream.getVideoTracks()[0];
    track.addEventListener('ended', () => {
      if (generation === current) stop('摄像头已断开，请检查连接。');
    });
    video.srcObject = stream;
    void video.play().catch(() => {});
    status.textContent = '载入手势识别模型…';
    if (!detector) detector = await loadDetector();
    if (generation !== current) return;
    status.textContent = '等待摄像头画面…';
    if (typeof MediaStreamTrackProcessor === 'function') {
      // Read the camera directly so a hidden preview tab cannot slow gesture sampling.
      frameReader = new MediaStreamTrackProcessor({ track, maxBufferSize: 1 }).readable.getReader();
      void trackFrames(frameReader, current);
    } else {
      videoTime = -1;
      timer = setInterval(trackPreview, frameInterval);
    }
    button.textContent = '关闭摄像头';
  } catch (error) {
    if (generation !== current) return;
    const failure = {
      NotAllowedError: ['浏览器未获得权限。允许摄像头后点下方按钮重试。', 'denied'],
      NotFoundError: ['系统未找到摄像头。接好设备后重试。', 'not-found'],
      NotReadableError: ['摄像头不可读。关闭可能占用它的程序后重试。', 'busy']
    }[error.name] || ['手势识别模型无法启动。请重开识别页。', 'model-error'];
    stop(...failure);
  } finally {
    button.disabled = !connected;
  }
}

button.addEventListener('click', () => stream ? stop('摄像头已关闭。') : void start());
window.addEventListener('pagehide', () => {
  releaseCamera();
  if (connected) void fetch('./state/off', { method: 'POST', keepalive: true }).catch(() => {});
});
void start();
