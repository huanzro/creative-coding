import { GestureGate, handOpenness } from './core.js';

const video = document.getElementById('preview');
const status = document.getElementById('status');
const button = document.getElementById('camera');
const gesture = new GestureGate();
let stream, detector, timer, queuedState;
let generation = 0, sending = false, connected = true;
let videoTime = -1, lastFrameAt = 0;

async function report(state) {
  queuedState = state;
  if (sending || !connected) return;
  sending = true;
  try {
    while (queuedState) {
      const next = queuedState;
      queuedState = null;
      const response = await fetch('./state/' + next, { method: 'POST', cache: 'no-store' });
      if (!response.ok) throw new Error('Unity connection lost');
    }
  } catch {
    connected = false;
    stop('Unity 已关闭连接。请回到 Unity 重新开启摄像头。');
    button.disabled = true;
  } finally {
    sending = false;
  }
}

function stop(message, state = 'off') {
  generation++;
  clearInterval(timer);
  stream?.getTracks().forEach(track => track.stop());
  stream = null;
  video.srcObject = null;
  gesture.reset();
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

function track() {
  const now = performance.now();
  if (video.readyState < 2 || videoTime === video.currentTime) {
    if (now - lastFrameAt > 350) {
      gesture.reset();
      status.textContent = '等待摄像头画面';
      void report('missing');
    }
    return;
  }
  videoTime = video.currentTime;
  lastFrameAt = now;
  try {
    const points = detector.detectForVideo(video, now).landmarks[0];
    const aspect = video.videoWidth / video.videoHeight;
    gesture.update(handOpenness(points?.map(point => ({ x: point.x * aspect, y: point.y, z: point.z * aspect }))), now);
    const state = gesture.state === 'unknown' ? 'missing' : gesture.state;
    status.textContent = { open: '已识别张手', closed: '已识别握拳', missing: '把一只手放进画面，先张开手掌' }[state];
    void report(state);
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
    stream.getVideoTracks()[0].addEventListener('ended', () => {
      if (generation === current) stop('摄像头已断开，请检查连接。');
    });
    video.srcObject = stream;
    await video.play();
    status.textContent = '载入手势识别模型…';
    if (!detector) detector = await loadDetector();
    if (generation !== current) return;
    videoTime = -1;
    lastFrameAt = performance.now();
    track();
    timer = setInterval(track, 85);
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
  generation++;
  clearInterval(timer);
  stream?.getTracks().forEach(track => track.stop());
  if (connected) void fetch('./state/off', { method: 'POST', keepalive: true }).catch(() => {});
});
void start();
