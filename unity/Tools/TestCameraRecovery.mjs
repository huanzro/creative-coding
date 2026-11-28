import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { runInNewContext } from 'node:vm';

const script = await readFile(new URL('../Assets/StreamingAssets/EchoesCamera/camera.mjs', import.meta.url), 'utf8');
const handlers = new Map();
const packets = [];
const tracks = [];
const timers = new Map();
let now = 0, timerId = 0, failNextPost = false, hangNextPost = false;

function eventTarget() {
  const listeners = new Map();
  return {
    addEventListener: (event, handler) => listeners.set(event, handler),
    fire: event => listeners.get(event)?.()
  };
}

function element(id) {
  if (!handlers.has(id)) handlers.set(id, { ...eventTarget(), textContent: '', readyState: 0, currentTime: 0,
    clientHeight: 0, querySelector: () => ({}), play: async () => {} });
  return handlers.get(id);
}

function schedule(callback, delay, repeating = false) {
  const id = ++timerId;
  timers.set(id, { callback, next: now + delay, repeat: repeating ? delay : 0 });
  return id;
}

const settle = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
async function advance(ms) {
  const goal = now + ms;
  for (;;) {
    const ready = [...timers.entries()].filter(([, item]) => item.next <= goal).sort((a, b) => a[1].next - b[1].next)[0];
    if (!ready) break;
    const [id, timer] = ready;
    now = timer.next;
    if (timer.repeat) timer.next += timer.repeat;
    else timers.delete(id);
    timer.callback();
    await settle();
  }
  now = goal;
}

runInNewContext(script.replace(/^import .*;\r?$/gm, '').replaceAll('import.meta.url', "'http://localhost/camera.mjs'"), {
  AbortController,
  document: { ...eventTarget(), hidden: false, getElementById: element },
  window: eventTarget(),
  performance: { now: () => now },
  setTimeout: schedule,
  setInterval: (callback, delay) => schedule(callback, delay, true),
  clearTimeout: id => timers.delete(id),
  clearInterval: id => timers.delete(id),
  navigator: { mediaDevices: { async getUserMedia() {
    const track = { ...eventTarget(), stopped: false, stop() { this.stopped = true; } };
    tracks.push(track);
    return { getVideoTracks: () => [track], getTracks: () => [track] };
  } } },
  async fetch(path, options) {
    packets.push(path);
    if (failNextPost) { failNextPost = false; throw new Error('Temporary connection reset'); }
    if (hangNextPost) {
      hangNextPost = false;
      await new Promise((_, reject) => options.signal.addEventListener('abort', () => reject(new Error('Deadline exceeded'))));
    }
    return { ok: true, status: 204 };
  },
  handSampleInterval: 1000 / 30,
  handOpenness: () => 2,
  loadHandDetector: async () => ({ detectForVideo: () => ({ landmarks: [[{ x: .5, y: .5, z: 0 }]] }) }),
  withIndexTip: state => state,
  IndexTipTracker: class { update() {} reset() {} }
}, { filename: 'camera.mjs' });

await settle();
const video = element('preview');
const status = element('status');
const cameraButton = element('camera');
video.readyState = 2;
video.videoWidth = 640;
video.videoHeight = 480;
assert.equal(tracks.length, 1);

async function frame() {
  video.currentTime++;
  video.fire('timeupdate');
  await settle();
}

failNextPost = true;
await frame();
assert.match(status.textContent, /恢复与 Unity/);
assert.equal(tracks[0].stopped, false);
await advance(150);
await frame();
await advance(150);
assert.equal(packets.at(-1), './state/open');
assert.match(status.textContent, /收到：张手/);

failNextPost = true;
await frame();
await advance(310);
assert.equal(packets.at(-1), './state/missing', 'Recovery does not replay a hand pose without a new camera frame');

hangNextPost = true;
await frame();
await advance(1550);
assert.equal(packets.at(-1), './state/missing', 'A stalled request times out and retries');
assert.equal(tracks[0].stopped, false);

tracks[0].fire('ended');
await settle();
assert.match(status.textContent, /正在重新连接/);
assert.equal(packets.at(-1), './state/recovering');
await advance(475);
assert.equal(tracks.length, 2);
await frame();

await advance(4150);
assert.equal(tracks.length, 3, 'A live track with no new frames is reacquired');
cameraButton.fire('click');
await settle();
await advance(5000);
assert.equal(tracks.length, 3, 'Explicit shutdown cancels automatic recovery');
assert.equal(tracks[2].stopped, true);
assert.equal(packets.at(-1), './state/off');
console.log('Passed camera bridge retry, stale-pose dropping, track disconnection, frozen captures and explicit shutdown.');
