import assert from 'node:assert/strict';
import { HandFrameClock, IndexTipTracker, indexTip, withIndexTip } from '../Assets/StreamingAssets/EchoesCamera/index-tip.mjs';

const aspect = 4 / 3;
const extended = [[0, -.065, -.01], [0, -.046, -.009], [0, -.031, -.006]];
const towardLens = [[.003, -.016, -.064], [.001, -.009, -.046], [.001, -.006, -.030]];
const curled = [[0, -.060, -.006], [0, .021, -.038], [0, .030, .010]];

function landmarks(index, middle = curled, ring = curled, little = curled) {
  const points = Array.from({ length: 21 }, () => ({ x: .5, y: .69, z: 0 }));
  for (const [finger, shape] of [index, middle, ring, little].entries()) {
    const root = 5 + 4 * finger;
    points[root] = { x: .36 + finger * .047, y: .61 + finger * .012, z: 0 };
    for (let i = 0; i < 3; i++) {
      const last = points[root + i];
      const [x, y, z] = shape[i];
      points[root + i + 1] = { x: last.x + x, y: last.y + y, z: last.z + z };
    }
  }
  return points;
}

const pointing = landmarks(extended);
const tip = indexTip(pointing, aspect);
assert.ok(tip);
assert.ok(Math.abs(tip.x - (1 - pointing[8].x)) < 1e-7 && Math.abs(tip.y - pointing[8].y) < 1e-7);
assert.ok(indexTip(landmarks(towardLens), aspect), 'A foreshortened index pointing toward the lens is still straight in 3D.');
const rolled = pointing.map(p => ({ x: .5 - (p.y - .5) / aspect, y: .5 + (p.x - .5) * aspect, z: p.z }));
assert.ok(indexTip(rolled, aspect), 'Finger angles work when the hand rolls sideways.');
assert.equal(indexTip(landmarks(curled), aspect), null, 'A fist lifts the pen.');
assert.equal(indexTip(landmarks(extended, extended, extended, extended), aspect), null, 'An open palm lifts the pen.');
assert.equal(indexTip(landmarks(extended, extended), aspect), null, 'A peace sign is not single-finger input.');
assert.equal(indexTip(undefined, aspect), null, 'A missing hand does not leave a marker.');

const tracker = new IndexTipTracker();
assert.equal(tracker.update(pointing, aspect, 10), null);
assert.deepEqual(tracker.update(pointing, aspect, 43), tip, 'Two consecutive 30 Hz samples put the pen down.');
assert.equal(withIndexTip('missing', tip), 'missing/6400/4680');
assert.deepEqual(tracker.update(null, aspect, 76), tip, 'A missing frame holds the active pen.');
assert.deepEqual(tracker.update(pointing, aspect, 109), tip, 'A short gap does not restart the stroke.');
assert.deepEqual(tracker.update(null, aspect, 175), tip);
assert.equal(tracker.update(null, aspect, 210), null, 'Sustained tracking loss lifts the pen.');
assert.equal(withIndexTip('open', null), 'open');
assert.equal(tracker.update(pointing, aspect, 243), null);
assert.deepEqual(tracker.update(pointing, aspect, 276), tip);
assert.equal(tracker.update(landmarks(extended, extended, extended, extended), aspect, 309), null, 'Opening the hand lifts immediately.');
tracker.update(pointing, aspect, 342);
tracker.update(pointing, aspect, 375);
assert.equal(tracker.update(landmarks(curled), aspect, 408), null, 'Closing the index lifts immediately.');
tracker.update(pointing, aspect, 441);
tracker.update(pointing, aspect, 474);
tracker.update(null, aspect, 507);
const moved = pointing.map(p => ({ ...p, x: p.x + .23 }));
assert.equal(tracker.update(moved, aspect, 540), null, 'A distant reacquisition starts a new stroke.');
assert.deepEqual(tracker.update(moved, aspect, 573), indexTip(moved, aspect));

for (const fps of [30, 60]) {
  const clock = new HandFrameClock();
  const accepted = Array.from({ length: fps * 3 }, (_, i) => i * 1000 / fps + Math.sin(i) * 1.3)
    .filter(time => clock.take(time));
  assert.ok(Math.abs(accepted.length - 90) <= 1, 'The gate stays near 30 Hz without throwing away alternating 30 Hz frames.');
  assert.ok(clock.take(10000), 'A capture stall resumes with the latest frame.');
  assert.equal(clock.take(10008), false, 'The clock does not burst through buffered frames after a stall.');
  clock.reset();
  assert.ok(clock.take(0), 'A new camera stream starts a fresh clock.');
}

console.log('Passed 30 Hz sampling, pointing gestures, dropout tolerance and deliberate pen lifts.');
