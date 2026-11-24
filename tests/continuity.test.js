import test from 'node:test';
import assert from 'node:assert/strict';
import {ContinuitySimulation} from '../dist/continuity.js';
import {comfortCalibration} from '../dist/interaction-controls.js';
import {VolumeEnvelope} from '../dist/volume-core.js';
const tick=(s,n=1)=>{for(let i=0;i<n;i++)s.update(1/60,true);};
const until=(s,check,n=5000)=>{for(let i=0;i<n&&!check();i++)tick(s);assert.ok(check(),s.phase+' / '+s.feedback);};
const voice=(s,n)=>{s.controlVoice=n;tick(s,2);};
const hand=(s,h)=>s.setHand(h);
function fresh(ch=0){const s=new ContinuitySimulation();s.reset(ch);s.tutorial=null;s.configureChallenge(true);hand(s,'open');voice(s,.3);return s;}
test('no hand means no progression; keyboard-like clicks cannot invent a gesture or sound',()=>{
 const s=new ContinuitySimulation();s.configureChallenge(true);tick(s,600);assert.equal(s.elapsed,0);assert.equal(s.voice,0);assert.equal(s.click(),false);assert.equal(s.setHand('closed'),false);assert.equal(s.hand,'unknown');
 hand(s,'open');tick(s,60);assert.equal(s.voice,0);assert.ok(Math.abs(s.learn.open-1)<.001);
});
test('real input tutorial requires both hand states, both comfortable voice regions, and one click',()=>{
 const s=new ContinuitySimulation();s.configureChallenge(true);hand(s,'open');tick(s,40);hand(s,'closed');tick(s,40);hand(s,'open');assert.equal(s.tutorial,1);
 voice(s,.25);tick(s,45);hand(s,'closed');voice(s,.7);tick(s,45);hand(s,'open');assert.equal(s.tutorial,2);
 s.click();hand(s,'closed');hand(s,'open');assert.equal(s.tutorial,null);assert.equal(s.phase,'gather');
});
test('forest safe and risky routes have different requirements, duration and reward',()=>{
 const totals=[];
 for(const route of [0,1]){
  const s=fresh();
  for(let r=0;r<3;r++){
   hand(s,'open');voice(s,.3);until(s,()=>s.phase==='fork');if(route)s.click();
   hand(s,'closed');hand(s,'open');const start=s.elapsed;
   if(route)hand(s,'closed');until(s,()=>s.phase==='nest');assert.ok(Math.abs(s.elapsed-start-(route?6:10))<.1);
   hand(s,'open');until(s,()=>s.round===r+1);
  }
  assert.ok(s.complete);totals.push(s.score);
 }
 assert.equal(totals[1]-totals[0],225);
});
test('unprotected gusts lose companions, while a nest lets the player recover locally',()=>{
 const s=fresh();until(s,()=>s.phase==='fork');s.click();hand(s,'closed');hand(s,'open');until(s,()=>s.phase==='nest');assert.equal(s.flock,2);
 until(s,()=>s.round===1);assert.equal(s.passed,1);assert.equal(s.score,300);
});
test('silence and loudness cannot substitute for the forest calming voice',()=>{
 const s=fresh();voice(s,0);tick(s,300);assert.equal(s.flock,0);voice(s,.9);tick(s,300);assert.equal(s.flock,0);voice(s,.25);until(s,()=>s.phase==='fork');assert.equal(s.flock,3);
});
function record(s,pattern){s.click();assert.equal(s.phase,'record');for(let i=0;i<pattern.length;i++){hand(s,pattern[i]);until(s,()=>s.recording.length===i+1);}assert.equal(s.phase,'replay-ready');}
test('ruins require a mixed recording and complementary replay across all three doors',()=>{
 const s=fresh(1);
 for(let r=0;r<3;r++){
  const pattern=Array.from({length:r+2},(_,i)=>i%2?'open':'closed');record(s,pattern);assert.deepEqual(s.recording,pattern);s.click();voice(s,.4);
  for(let i=0;i<pattern.length;i++){hand(s,pattern[i]==='open'?'closed':'open');until(s,()=>s.slot===i+1);}
  assert.equal(s.phase,'cross');until(s,()=>s.round===r+1);
 }
 assert.ok(s.complete);
});
test('ruins reject a constant recording and preserve valid recordings after failed cooperation',()=>{
 const s=fresh(1);s.click();tick(s,250);assert.equal(s.phase,'record-ready');assert.match(s.feedback,/只有一种/);
 record(s,['closed','open']);s.click();hand(s,'closed');voice(s,.4);until(s,()=>s.phase==='failed');assert.deepEqual(s.recording,['closed','open']);s.click();assert.equal(s.phase,'replay');
});
function sendWave(s){const w=s.objects[0],desired=w.kind==='storm'?1:0;if(s.color!==desired)s.click();hand(s,'open');hand(s,'closed');voice(s,w.kind==='storm'?w.amp:1-w.amp);tick(s,60);assert.ok(s.waveReady);hand(s,'open');}
test('star waves actually travel, combine amplitudes and finish with a response to the player',()=>{
 const s=fresh(2);
 for(let r=0;r<3;r++){
  const count=[2,3,2][r];
  for(let w=0;w<count;w++){
   sendWave(s);assert.ok(s.pending);assert.equal(s.phase,'wave');until(s,()=>s.phase==='resolved');assert.ok(s.lastResult.good);
   until(s,()=>s.phase!=='resolved');
  }
  if(r<2)assert.equal(s.round,r+1);
 }
 assert.equal(s.phase,'compose');
 for(const level of [.2,.7,.4]){hand(s,'closed');voice(s,level);tick(s,60);s.click();hand(s,'open');}
 assert.equal(s.phase,'answer');assert.equal(s.userPhrase.length,3);until(s,()=>s.complete);assert.equal(s.passed,3);
});
test('wrong phase leaves a measurable residual and retries the same incoming wave',()=>{
 const s=fresh(2);hand(s,'closed');voice(s,.45);tick(s,60);assert.equal(s.waveReady,false);hand(s,'open');until(s,()=>s.phase==='wave-retry');assert.ok(s.lastResult.result>.8);assert.equal(s.waveIndex,0);until(s,()=>s.phase==='wave');assert.equal(s.objects[0].amp,.45);
});
test('input loss clears charge without sending a phantom response and freezes the encounter',()=>{
 const s=fresh(2);hand(s,'closed');voice(s,.7);tick(s,30);s.cancelInput();const time=s.elapsed,x=s.objects[0].x;tick(s,300);assert.equal(s.pending,null);assert.equal(s.elapsed,time);assert.equal(s.objects[0].x,x);assert.equal(s.setHand('closed'),false);hand(s,'open');assert.equal(s.pending,null);
});
test('comfortable calibration rejects indistinguishable noise and adapts to a quiet speaker',()=>{
 assert.equal(comfortCalibration([-50,-49,-50],[-49,-50,-49]),null);
 assert.equal(comfortCalibration([],[-35,-35,-35]),null);
 const c=comfortCalibration([-65,-66,-65],[-36,-35,-34]);
 assert.ok(c.quietDb<-50);assert.ok(c.loudDb>-34);assert.equal(c.release,.28);
 const envelope=new VolumeEnvelope(c);
 for(let i=0;i<90;i++)envelope.update(-35,1/60);
 assert.ok(envelope.level>.55&&envelope.level<.9,'the calibration hum must work in the tutorial');
});
test('save version migration is explicit; completed progress restores without synthetic input',()=>{
 const s=fresh();s.round=s.passed=1;s.score=300;const t=new ContinuitySimulation();assert.ok(t.restore(s.save()));assert.equal(t.round,1);assert.equal(t.hand,'unknown');assert.equal(t.restore({...s.save(),version:1}),false);assert.equal(t.restore({...s.save(),round:99}),false);
});
test('finished chapters can reacquire a real hand before the next physical click',()=>{
 const s=fresh();s.complete=true;s.cancelInput();assert.equal(s.click(),false);hand(s,'open');assert.equal(s.click(),'next');
});
test('invalid stored wave entries do not crash recovery or replace a valid gesture with a default',()=>{
 const s=fresh(2);
 assert.equal(s.restore({...s.save(),phrase:[null]}),true);
 assert.deepEqual(s.userPhrase,[]);
 assert.equal(s.hand,'unknown');
 assert.equal(s.restore({...s.save(),chapter:0,round:3,tutorial:0}),false);
});
test('ruins report a voice-only miss when the complementary hand was held in time',()=>{
 const s=fresh(1);
 record(s,['closed','open']);s.click();voice(s,.02);
 hand(s,'open');until(s,()=>s.phase==='failed');
 assert.match(s.feedback,/声音在亮区停留不足/);
 assert.doesNotMatch(s.feedback,/相反动作保持时间不足/);
 assert.deepEqual(s.recording,['closed','open']);
});
test('the closing encounter stays complete on an extra click or retry',()=>{
 const s=fresh(2);
 assert.ok(s.restore({...s.save(),round:3,score:900,phrase:[{amp:.4,sign:-1}]}));
 hand(s,'open');
 assert.equal(s.click(),false);
 s.retry();
 assert.equal(s.complete,true);
 assert.equal(s.round,3);
 assert.deepEqual(s.userPhrase,[{amp:.4,sign:-1}]);
});
