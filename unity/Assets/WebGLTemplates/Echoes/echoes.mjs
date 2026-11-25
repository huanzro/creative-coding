/* Only the browser can grant device permissions. Unity owns the journey and
   sends render state back for the HTML controls and the second display. */
import { GestureGate, handOpenness } from './browser/core.js';
import { IndexTipTracker, withIndexTip } from './StreamingAssets/EchoesCamera/index-tip.mjs';
import { MicrophoneInput } from './browser/audio-input.js';
import { VolumeEnvelope, volumeSettings } from './browser/volume-core.js';
import { comfortCalibration } from './browser/interaction-controls.js';
import { ExhibitSerial } from './browser/exhibit-serial.js';
import { TouchLampCue } from './browser/exhibit-game.js';
import { loadInputProfile, saveInputProfile } from './browser/input-profiles.js';

const ui = id => document.getElementById(id);
const canvas = ui('unity-canvas');
const projectionId = new URLSearchParams(location.search).get('projection');
const projection = Boolean(projectionId);
const room = projectionId || [...crypto.getRandomValues(new Uint32Array(3))].join('-');
const channel = 'BroadcastChannel' in window ? new BroadcastChannel('echoes-unity-' + room) : null;
const gesture = new GestureGate();
const indexTracker = new IndexTipTracker();
const volume = new VolumeEnvelope();
const source = new MicrophoneInput(stopAudio);
const video = ui('input-video');
const exhibit = new ExhibitSerial();
const cue = new TouchLampCue();
const defaultHand = { openThreshold: gesture.openThreshold, closedThreshold: gesture.closedThreshold };
let unity, state, stream, detector, projectionWindow, audioCalibration, handCalibration;
let cameraGeneration = 0, lastInference = 0, lastVideoTime = -1, lastHandFrame = 0;
let audioReady = false, lastTick = 0, mirror = false;

function send(method, value = '') {
  unity?.SendMessage('EchoesPlayer', method, String(value));
}

function text(id, value) {
  const element = ui(id);
  if (element.textContent !== value) element.textContent = value;
}

const canPlay = () => state && !state.paused && !document.hidden && (!state.waiting || state.complete);
const canOutput = () => canPlay() && !state.preview;

function setHardwareStatus(message) {
  text('hardware-status', message);
  ui('arm').disabled = !exhibit.ready;
  ui('fan').disabled = !exhibit.ready || !exhibit.fanSupported;
  text('serial', exhibit.port ? '断开 Arduino' : '连接 Arduino');
}

function stopOutputs() {
  if (exhibit.armed) exhibit.setArmed(false);
  cue.clear();
  ui('arm').checked = ui('fan').checked = false;
}

function updateOutputs(now) {
  if (!exhibit.armed) return;
  const lamps = ui('arm').checked ? cue.output(now) : {};
  const fan = ui('fan').checked ? Math.round(state.voice * 132) : 0;
  exhibit.setOutputs({ ...lamps, fan });
}

function choose(option, momentary) {
  if (!canPlay()) return;
  if (momentary || state.preview) send('Choose', option);
  else send('BrowserTouch', option * 2 + 1);
  if (exhibit.armed && canOutput() && ui('arm').checked) {
    const touch = option === 0 ? 'touchA' : 'touchB';
    const now = performance.now();
    cue.start(touch, now);
    if (momentary) cue.release(touch, now);
    updateOutputs(now);
    exhibit.flush();
  }
  if (state.preview) canvas.focus();
}

function releaseChoice(option) {
  send('BrowserTouch', option * 2);
}

function bindChoice(id, option) {
  const button = ui(id);
  button.onclick = event => {
    if (!state?.spirit?.ruins || state.preview || event.detail === 0) choose(option, true);
  };
  button.onpointerdown = event => {
    if (event.button !== 0 || !canPlay() || !state.spirit?.ruins || state.preview) return;
    button.setPointerCapture(event.pointerId);
    choose(option, false);
  };
  button.onlostpointercapture = () => releaseChoice(option);
}

function stopCamera() {
  cameraGeneration++;
  stream?.getTracks().forEach(track => track.stop());
  stream = null;
  video.srcObject = null;
  handCalibration = null;
  ui('calibrate-hand').disabled = true;
  gesture.reset();
  indexTracker.reset();
  send('BrowserHand', 'unknown');
  send('BrowserCameraReady', 0);
  text('camera', '开启摄像头');
  text('hand-status', '摄像头已关闭');
}

async function toggleCamera() {
  if (stream) { stopCamera(); return; }
  const generation = ++cameraGeneration;
  ui('camera').disabled = true;
  try {
    if (!navigator.mediaDevices) throw new Error('请通过 HTTPS 或 localhost 打开。');
    const media = await navigator.mediaDevices.getUserMedia({
      video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' }, audio: false
    });
    if (generation !== cameraGeneration) { media.getTracks().forEach(track => track.stop()); return; }
    stream = media;
    stream.getVideoTracks()[0].addEventListener('ended', () => {
      if (generation === cameraGeneration) stopCamera();
    });
    video.srcObject = stream;
    await video.play();
    text('hand-status', '载入手势识别模型…');
    if (!detector) {
      const { FilesetResolver, HandLandmarker } = await import('./browser/vendor/vision_bundle.mjs');
      const vendor = new URL('./browser/vendor/', import.meta.url);
      const files = await FilesetResolver.forVisionTasks(new URL('wasm', vendor).href);
      detector = await HandLandmarker.createFromOptions(files, {
        baseOptions: { modelAssetPath: new URL('hand_landmarker.task', vendor).href, delegate: 'CPU' },
        runningMode: 'VIDEO', numHands: 1, minHandDetectionConfidence: .65,
        minHandPresenceConfidence: .65, minTrackingConfidence: .6
      });
    }
    if (generation !== cameraGeneration) return;
    const saved = loadInputProfile('hand', stream.getVideoTracks()[0]);
    const valid = Number.isFinite(saved?.openThreshold) && Number.isFinite(saved?.closedThreshold) &&
      saved.closedThreshold > 0 && saved.openThreshold > saved.closedThreshold;
    gesture.openThreshold = valid ? saved.openThreshold : defaultHand.openThreshold;
    gesture.closedThreshold = valid ? saved.closedThreshold : defaultHand.closedThreshold;
    lastVideoTime = -1;
    lastHandFrame = performance.now();
    ui('calibrate-hand').disabled = false;
    send('BrowserCameraReady', 1);
    text('camera', '关闭摄像头');
    text('hand-status', '把一只手放入画面，先张开');
  } catch (error) {
    if (generation !== cameraGeneration) return;
    stopCamera();
    text('hand-status', '摄像头未就绪。' + error.message);
  } finally { ui('camera').disabled = false; }
}

function track(now) {
  if (!stream || !detector || now - lastInference < 70) return;
  lastInference = now;
  if (video.readyState < 2 || video.currentTime === lastVideoTime) {
    if (now - lastHandFrame > 300) {
      gesture.reset();
      indexTracker.reset();
      send('BrowserHand', 'unknown');
      text('hand-status', '等待摄像头画面');
    }
    return;
  }
  lastVideoTime = video.currentTime;
  lastHandFrame = now;
  try {
    const points = detector.detectForVideo(video, now).landmarks[0];
    const aspect = video.videoWidth / video.videoHeight;
    const score = handOpenness(points?.map(p => ({ x: p.x * aspect, y: p.y, z: p.z * aspect })));
    if (handCalibration) { calibrateHand(score, now); return; }
    gesture.update(score, now);
    const tip = indexTracker.update(points, aspect, now);
    send('BrowserHand', withIndexTip(gesture.state, tip));
    text('hand-status', tip ? '已识别食指位置' :
      { open: '已识别张手', closed: '已识别握拳', unknown: '请把手放回画面，先张开' }[gesture.state]);
  } catch (error) {
    stopCamera();
    text('hand-status', '手势识别中断。' + error.message);
  }
}

function calibrateHand(score, now) {
  const calibration = handCalibration;
  const learningOpen = calibration.open === null;
  const instruction = learningOpen ? '张开手掌' : '握拳';
  text('hand-status', '校准手势 · ' + instruction + '并保持约 1 秒');
  if (!Number.isFinite(score) || (learningOpen ? score < 1.5 : score > calibration.open - .35)) {
    calibration.started = 0;
    calibration.samples.length = 0;
    return;
  }
  if (calibration.started === 0) calibration.started = now;
  calibration.samples.push(score);
  if (now - calibration.started < 1000 || calibration.samples.length < 10) return;
  const median = calibration.samples.sort((a, b) => a - b)[Math.floor(calibration.samples.length / 2)];
  if (learningOpen) {
    handCalibration = { open: median, started: 0, samples: [] };
    return;
  }
  gesture.openThreshold = median + (calibration.open - median) * .72;
  gesture.closedThreshold = median + (calibration.open - median) * .28;
  gesture.reset();
  handCalibration = null;
  saveInputProfile('hand', stream?.getVideoTracks()[0], {
    openThreshold: gesture.openThreshold, closedThreshold: gesture.closedThreshold
  });
  send('BrowserCameraReady', 1);
  text('hand-status', '手势校准完成。请先张手。');
}

function stopAudio() {
  source.stop();
  audioCalibration = null;
  audioReady = false;
  volume.reset();
  send('BrowserAudioReady', 0);
  send('BrowserVoice', 0);
  ui('calibrate-mic').disabled = true;
  text('mic', '开启麦克风');
  text('mic-status', '麦克风已关闭');
}

async function toggleAudio() {
  if (source.active) { stopAudio(); return; }
  ui('mic').disabled = true;
  try {
    if (!await source.start()) return;
    const saved = loadInputProfile('mic', source.stream?.getAudioTracks()[0]);
    if (Number.isFinite(saved?.quietDb) && Number.isFinite(saved?.loudDb) && saved.loudDb >= saved.quietDb + 6) {
      volume.configure(volumeSettings(saved));
      volume.reset();
      audioReady = true;
      send('BrowserAudioReady', 1);
    } else beginAudioCalibration();
    ui('calibrate-mic').disabled = false;
    text('mic', '关闭麦克风');
  } catch (error) { text('mic-status', '麦克风未就绪。' + error.message); }
  finally { ui('mic').disabled = false; }
}

function beginAudioCalibration() {
  if (!source.active) return;
  audioReady = false;
  send('BrowserAudioReady', 0);
  audioCalibration = { start: performance.now(), quiet: [], comfortable: [] };
}

function sampleAudio(now, dt) {
  if (!source.active) return;
  const db = source.readDb();
  if (audioCalibration) {
    const age = now - audioCalibration.start;
    if (age < 2000) {
      audioCalibration.quiet.push(db);
      text('mic-status', '保持安静 ' + Math.ceil((2000 - age) / 1000) + ' 秒');
      return;
    }
    if (age < 5000) {
      if (age > 2300) audioCalibration.comfortable.push(db);
      text('mic-status', '轻松地哼声 ' + Math.ceil((5000 - age) / 1000) + ' 秒');
      return;
    }
    const settings = comfortCalibration(audioCalibration.quiet, audioCalibration.comfortable);
    audioCalibration = null;
    if (!settings) {
      stopAudio();
      text('mic-status', '没有测到明显的声音变化。可以重新校准。');
      return;
    }
    volume.configure(settings);
    volume.reset();
    saveInputProfile('mic', source.stream?.getAudioTracks()[0], settings);
    audioReady = true;
    send('BrowserAudioReady', '1');
  }
  if (audioReady) {
    const { level } = volume.update(db, dt);
    send('BrowserVoice', level.toFixed(3));
    text('mic-status', '音量 ' + Math.round(level * 100) + '%');
  }
}

function tick(now) {
  if (projectionWindow?.closed) {
    projectionWindow = null;
    send('SetSplit', 0);
  }
  if (!document.hidden) {
    track(now);
    if (now - lastTick >= 58) {
      sampleAudio(now, Math.min(.1, (now - lastTick) / 1000));
      lastTick = now;
    }
    if (canOutput() && !state.complete) updateOutputs(now);
    else stopOutputs();
  }
  requestAnimationFrame(tick);
}

function configurePrimary() {
  window.EchoesReceiveFrame = json => {
    state = JSON.parse(json);
    text('title', state.title);
    text('cue', state.message);
    text('chapter-number', state.lesson >= 0 ? '练习 ' + (state.lesson + 1) + ' / 3' :
      state.chapter === 2 ? '第 3 章 · 指尖绘画' : '第 ' + (state.chapter + 1) + ' 章' +
      (state.phase === 'Gather' || state.phase === 'Nest' ? ' · 萤火 ' + state.flock + ' / ' + state.quota :
        state.complete ? ' · ' + state.score + ' 分' : ''));
    text('choice-a', state.left);
    text('choice-b', state.right);
    text('pause', state.paused ? '继续' : '暂停');
    ui('progress').value = state.progress;
    ui('voice').value = state.voice;
    const enabled = canPlay() && !(state.chapter === 2 && state.complete);
    ui('choice-a').disabled = ui('choice-b').disabled = !enabled;
    ui('choice-a').setAttribute('aria-pressed', String(state.choice === 0 && state.left.startsWith('A ·')));
    ui('choice-b').setAttribute('aria-pressed', String(state.choice === 1 && state.right.startsWith('B ·')));
    if (projectionWindow && !projectionWindow.closed) channel?.postMessage(json);
  };
  bindChoice('choice-a', 0);
  bindChoice('choice-b', 1);
  ui('camera').onclick = toggleCamera;
  ui('mic').onclick = toggleAudio;
  ui('calibrate-mic').onclick = beginAudioCalibration;
  ui('calibrate-hand').onclick = () => {
    if (!stream) return;
    handCalibration = { open: null, started: 0, samples: [] };
    gesture.reset();
    indexTracker.reset();
    send('BrowserHand', 'unknown');
    send('BrowserCameraReady', 0);
    stopOutputs();
  };
  ui('preview').onclick = () => {
    const next = ui('preview').getAttribute('aria-pressed') === 'false';
    ui('preview').setAttribute('aria-pressed', String(next));
    send('SetPreview', Number(next));
    if (next) {
      stopCamera();
      stopAudio();
      stopOutputs();
      text('hand-status', state?.chapter === 2 ? '拖动鼠标画光。A / B 放出图案。' : '空格握拳，松开张手。A / B 选项。');
      text('mic-status', 'Z 轻声，X 舒适发声。');
      canvas.focus();
    }
  };
  for (const button of document.querySelectorAll('[data-chapter]'))
    button.onclick = () => send('Select', button.dataset.chapter);
  ui('retry').onclick = () => send('Retry');
  ui('restore').onclick = () => send('Restore');
  ui('time-of-day').onchange = event => send('SetTimeOfDay', event.target.value);
  ui('scroll-speed').oninput = event => send('SetScrollSpeed', event.target.value);
  ui('pause').onclick = () => { stopOutputs(); send('Pause', Number(!state?.paused)); };
  ui('project').disabled = !channel;
  ui('project').onclick = () => {
    const url = new URL(location.href);
    url.searchParams.set('projection', room);
    projectionWindow = window.open(url, 'echoes_projection_' + room, 'popup,width=960,height=540');
  };
  channel?.addEventListener('message', ({ data }) => {
    if (data === 'ready' && state && projectionWindow && !projectionWindow.closed) {
      send('SetSplit', 1);
      channel.postMessage(JSON.stringify(state));
    }
  });
  exhibit.addEventListener('status', ({ detail }) => {
    if (!exhibit.ready) { releaseChoice(0); releaseChoice(1); }
    setHardwareStatus(detail);
  });
  exhibit.addEventListener('armed', ({ detail }) => {
    if (!detail) { ui('arm').checked = ui('fan').checked = false; cue.clear(); }
    setHardwareStatus(exhibit.ready ? 'EOL3 已连接' : '等待 EOL3 固件');
  });
  exhibit.addEventListener('fan', () => setHardwareStatus(exhibit.fanSupported
    ? '组合固件已启用风扇' : '等待组合固件'));
  exhibit.addEventListener('touch', ({ detail }) => {
    if (!['touchA', 'touchB'].includes(detail.source)) return;
    const option = detail.source === 'touchA' ? 0 : 1;
    if (detail.pressed) choose(option, false);
    else {
      releaseChoice(option);
      cue.release(detail.source, performance.now());
    }
  });
  ui('serial').onclick = async () => {
    ui('serial').disabled = true;
    try {
      if (exhibit.port) await exhibit.disconnect();
      else await exhibit.connect();
    } catch (error) { setHardwareStatus('串口连接未完成。' + error.message); }
    finally { ui('serial').disabled = false; }
  };
  ui('arm').onchange = ui('fan').onchange = () => {
    if (!canOutput() || state.complete) { stopOutputs(); return; }
    if (!ui('arm').checked) cue.clear();
    exhibit.setArmed(ui('arm').checked || ui('fan').checked);
    updateOutputs(performance.now());
    exhibit.flush();
  };
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) { stopOutputs(); send('BrowserHand', 'unknown'); }
  });
  window.addEventListener('pagehide', () => {
    stopOutputs();
    stopCamera();
    stopAudio();
    detector?.close();
    void exhibit.disconnect();
    channel?.close();
  });
}

function configureProjection() {
  document.body.classList.add('projection');
  ui('projection-controls').hidden = false;
  channel?.addEventListener('message', ({ data }) => {
    if (typeof data === 'string' && data !== 'ready') send('ProjectFrame', data);
  });
  window.addEventListener('keydown', event => {
    if (event.key.toLowerCase() === 'm') { mirror = !mirror; send('SetMirror', Number(mirror)); }
    if (event.key.toLowerCase() === 'h') ui('projection-controls').hidden = !ui('projection-controls').hidden;
  });
  canvas.addEventListener('dblclick', () => {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void document.documentElement.requestFullscreen();
  });
}

if (projection) configureProjection();
else configurePrimary();
try {
  const { loader, ...config } = window.echoesBuild;
  await new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = loader;
    script.onload = resolve;
    script.onerror = () => reject(new Error('无法读取 Unity 加载器。'));
    document.head.append(script);
  });
  unity = await window.createUnityInstance(canvas, config, progress => {
    text('loading', '正在载入 Unity · ' + Math.round(progress * 100) + '%');
  });
  ui('loading').hidden = true;
  if (projection) {
    send('SetProjection', 1);
    channel?.postMessage('ready');
  } else {
    ui('controls').hidden = ui('overlay').hidden = false;
    requestAnimationFrame(tick);
  }
} catch (error) {
  text('loading', 'Unity 无法启动。' + error.message);
}
