import test from 'node:test';
import assert from 'node:assert/strict';
import {TouchLampCue} from '../dist/exhibit-game.js';
import {encodeExhibitOutput, OFF} from '../dist/exhibit-serial.js';

test('quick touches remain visible, later touches use only their own lamp, and a held touch expires', () => {
  const cue = new TouchLampCue();
  cue.start('touchA', 0);
  cue.release('touchA', 30);
  assert.deepEqual(cue.output(500), {lampA: [12, 4, 0], lampB: [0, 0, 0], servo: null});
  assert.ok(cue.output(710).lampA[0] < 12);
  cue.start('touchB', 750);
  cue.release('touchA', 760);
  assert.deepEqual(cue.output(1000), {lampA: [0, 0, 0], lampB: [0, 3, 12], servo: null});
  assert.deepEqual(cue.output(3000), OFF);
});

test('each touch drives its own lamp independently and the game leaves the servo released', () => {
  const cue = new TouchLampCue();
  cue.start('touchA', 0);
  cue.start('touchB', 200);
  cue.release('touchA', 250);
  assert.equal(encodeExhibitOutput(1,cue.output(500)),'EOL3 SET 1 12 4 0 0 3 12 -1\n');
  assert.deepEqual(cue.output(930),{lampA:[0,0,0],lampB:[0,3,12],servo:null});
  cue.clear();
  assert.deepEqual(cue.output(950),OFF);
});
