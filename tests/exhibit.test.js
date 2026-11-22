import test from 'node:test';
import assert from 'node:assert/strict';
import {ExhibitSerial, encodeExhibitOutput, readExhibitFrame} from '../dist/exhibit-serial.js';

test('EOL3 represents optional touch inputs separately and rejects damaged frames', () => {
  assert.deepEqual(readExhibitFrame('EOL3 IN 5 5'), {
    type: 'inputs', inputs: {touchA: true, touchB: null, fabric: true},
  });
  for (const line of ['EOL3 IN 8 7', 'EOL3 IN 5 9', 'EOL3 ACK 65536', 'EOL2 IN 0 7']) {
    assert.equal(readExhibitFrame(line), null);
  }
  assert.equal(
    encodeExhibitOutput(42, {lampA: [255, -1, 2.6], lampB: [NaN, 9, 0], servo: 170}),
    'EOL3 SET 42 64 0 3 0 9 0 120\n',
  );
});

test('serial arming requires EOL3, touch reports both edges, and disarming clears outputs', async () => {
  const writes = [];
  let input;
  const port = {
    readable: new ReadableStream({start(controller) { input = controller; }}),
    writable: new WritableStream({write(bytes) { writes.push(new TextDecoder().decode(bytes)); }}),
    async open({baudRate}) { assert.equal(baudRate, 115200); },
    async close() {},
  };
  const exhibit = new ExhibitSerial({async requestPort() { return port; }});
  const touch = [];
  exhibit.addEventListener('touch', ({detail}) => touch.push(detail));
  const enqueue = frame => input.enqueue(new TextEncoder().encode(frame));
  const pump = () => new Promise(resolve => setImmediate(resolve));
  await exhibit.connect();
  try {
    exhibit.setArmed(true);
    assert.equal(exhibit.armed, false);
    enqueue('EOL3 IN 0');
    enqueue(' 5\nEOL3 IN 1 5\nEOL3 IN 0 5\n');
    await pump();
    assert.equal(exhibit.ready, true);
    assert.deepEqual(touch, [{source: 'touchA', pressed: true}, {source: 'touchA', pressed: false}]);
    exhibit.setArmed(true);
    exhibit.setOutputs({lampB: [2, 16, 30], servo: 90});
    exhibit.flush();
    await exhibit.writing;
    assert.match(writes.at(-1), / 0 0 0 2 16 30 90\n$/);
    exhibit.setArmed(false);
    await exhibit.writing;
    assert.match(writes.at(-1), / 0 0 0 0 0 0 -1\n$/);
  } finally {
    await exhibit.disconnect();
  }
  assert.equal(exhibit.port, null);
});
