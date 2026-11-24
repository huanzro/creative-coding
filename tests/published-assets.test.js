import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import crypto from 'node:crypto';
import {encodeExhibitOutput,readExhibitFrame,ExhibitSerial,OFF} from '../dist/exhibit-serial.js';
import {outputLevels,encodeOutput,OUTPUT_ZERO} from '../dist/physical-output.js';
import {TouchLampCue} from '../dist/exhibit-game.js';
import {VolumeEnvelope,ambientCalibration} from '../dist/volume-core.js';
import {GestureGate} from '../dist/core.js';
import {ContinuitySimulation} from '../dist/continuity.js';
test('published artwork, spirit renderer, projection animation and physical protocols are unchanged',()=>{
 const baseline=JSON.parse(fs.readFileSync(new URL('./published-assets-baseline.json',import.meta.url)));
 const migrated=new Set(['app.js','index.html','style.css','interaction-controls.js','exhibit-game.js','atmosphere.js','exhibit-serial.js']);
 for(const name of Object.keys(baseline.files).filter(name=>!migrated.has(name))){
  const file=fs.readFileSync(new URL('../dist/'+name,import.meta.url));
  const hashes=[file];
  // Windows Git may convert text line endings. Artwork and models stay byte-exact.
  if(/\.(js|mjs|css|html|json)$|(^|\/)LICENSE$/.test(name)){
   const lf=file.toString('utf8').replaceAll('\r\n','\n');
   hashes.push(lf,lf.replaceAll('\n','\r\n'));
  }
  assert.ok(hashes.some(data=>crypto.createHash('sha256').update(data).digest('hex')===baseline.files[name]),name);
 }
});
test('EOL3 input edges and bounded dual lamp / servo packets preserve the exhibition protocol',()=>{
 assert.deepEqual(readExhibitFrame('EOL3 IN 1 3'),{type:'inputs',inputs:{touchA:true,touchB:false,fabric:null}});
 assert.equal(readExhibitFrame('EOL3 IN 9 3'),null);
 assert.equal(encodeExhibitOutput(8,{lampA:[100,4,0],lampB:[0,3,12],servo:300}),'EOL3 SET 8 64 4 0 0 3 12 120\n');
 const serial=new ExhibitSerial();const events=[];serial.addEventListener('touch',e=>events.push(e.detail));
 serial.handleFrame(readExhibitFrame('EOL3 IN 0 3'));assert.equal(events.length,0);
 serial.handleFrame(readExhibitFrame('EOL3 IN 1 3'));serial.handleFrame(readExhibitFrame('EOL3 IN 1 3'));serial.handleFrame(readExhibitFrame('EOL3 IN 0 3'));
 assert.deepEqual(events,[{source:'touchA',pressed:true},{source:'touchA',pressed:false}]);assert.equal(serial.armed,false);
});
test('lamp cues release and expire; inactive legacy outputs stay zero',()=>{
 const cue=new TouchLampCue();cue.start('touchA',0);assert.deepEqual(cue.output(100).lampA,[12,4,0]);cue.release('touchA',200);assert.deepEqual(cue.output(1000),OFF);
 assert.deepEqual(outputLevels({active:false,speed:2,glow:1}),OUTPUT_ZERO);assert.match(encodeOutput(1,OUTPUT_ZERO),/^EOL2 SET 1 0 0 0 0 0 800\n$/);
});
test('original calibrated microphone envelope feeds gentle and strong responses',()=>{
 const envelope=new VolumeEnvelope(ambientCalibration([-60,-59,-60]));for(let i=0;i<120;i++)envelope.update(-60,1/60);assert.ok(envelope.level<.01);
 for(let i=0;i<120;i++)envelope.update(-10,1/60);assert.ok(envelope.level>.95);
});
test('stable hand states are accepted once, and loss requires a fresh open palm',()=>{
 const g=new GestureGate(),s=new ContinuitySimulation();s.configureChallenge(true);
 for(const [score,time] of [[2,0],[2,200],[1,300],[1,500],[1,900]]){g.update(score,time);if(['open','closed'].includes(g.state))s.setHand(g.state);s.update(.04,true);}
 assert.equal(s.hand,'closed');assert.equal(s.holding,true);s.cancelInput();g.reset();g.update(1,1000);g.update(1,1200);assert.equal(s.setHand(g.state),false);assert.equal(s.hand,'unknown');
});
