import test from 'node:test';
import assert from 'node:assert/strict';
import {rmsDb,VolumeEnvelope,volumeSettings,ambientCalibration} from '../dist/volume-core.js';
import {SpiritSimulation} from '../dist/core.js';
import {outputLevels,encodeOutput,SerialOutput} from '../dist/physical-output.js';

test('RMS distinguishes amplitude rather than pitch and removes DC offset',()=>{
  const tone=(amplitude,hz,offset=0)=>Float32Array.from({length:4800},(_,i)=>offset+amplitude*Math.sin(i*2*Math.PI*hz/48000));
  assert.equal(rmsDb(new Float32Array(2048)),-100);
  assert.ok(Math.abs(rmsDb(tone(.1,200))-rmsDb(tone(.1,800)))<.01);
  assert.ok(Math.abs(rmsDb(tone(.2,200))-rmsDb(tone(.1,200))-6.0206)<.01);
  assert.ok(Math.abs(rmsDb(tone(.1,200,.3))-rmsDb(tone(.1,200)))<.01);
  assert.equal(rmsDb([NaN,1]),-100);
});
test('volume response is smooth, frame-rate independent, bounded and returns to silence',()=>{
  const a=new VolumeEnvelope(),b=new VolumeEnvelope();
  for(let i=0;i<30;i++)a.update(-18,1/30);for(let i=0;i<120;i++)b.update(-18,1/120);
  assert.ok(Math.abs(a.speed-b.speed)<1e-9);assert.ok(a.speed>2&&a.speed<=2.2);
  const first=new VolumeEnvelope().update(0,1/60);assert.ok(first.speed>0&&first.speed<.3);
  for(let i=0;i<600;i++)a.update(-100,1/60);assert.equal(a.speed,0);
  assert.ok(new VolumeEnvelope().update(NaN,.1).speed===0);
});
test('bad thresholds are repaired, ambient calibration ignores isolated peaks',()=>{
  const settings=volumeSettings({quietDb:-20,loudDb:-60,maxSpeed:99,release:NaN});
  assert.ok(settings.loudDb>=settings.quietDb+6);assert.equal(settings.maxSpeed,3);assert.equal(settings.release,.65);
  const result=ambientCalibration([...Array(59).fill(-55),-2]);assert.equal(result.quietDb,-49);
});
test('sound speed changes world, targets and trail equally without moving actor off center',()=>{
  const make=speed=>{const s=new SpiritSimulation();s.reset(0,'gather');s.configureChallenge(true);s.started=true;s.speedMultiplier=speed;s.targets.forEach(t=>t.x+=5);return s;};
  const slow=make(.5),fast=make(2),silent=make(0);const x=slow.targets[0].x;
  for(let i=0;i<60;i++){slow.update(1/60,true);fast.update(1/60,true);silent.update(1/60,true);}
  assert.ok(Math.abs(fast.distance/slow.distance-4)<1e-9);
  assert.ok(Math.abs((x-fast.targets[0].x)/(x-slow.targets[0].x)-4)<1e-9);
  assert.equal(silent.distance,0);assert.equal(silent.targets[0].x,x);
  for(const s of [slow,fast,silent])assert.equal(s.trail.at(-1).x,.5);
});
test('trace emission count is display-rate independent and memory remains bounded',()=>{
  const run=rate=>{const s=new SpiritSimulation();s.started=true;s.soundEnergy=1;for(let i=0;i<rate*10;i++)s.update(1/rate);return s;};
  const a=run(30),b=run(120);assert.ok(Math.abs(a.trail.length-b.trail.length)<=1);
  assert.ok(a.trail.length<=450&&b.particles.length<=200);
});
test('physical preview has limits, scene colours and zero state',()=>{
  assert.equal(outputLevels({active:false,speed:3,pulse:1}).fan,0);
  const v=outputLevels({active:true,speed:99,glow:99,pulse:99});assert.ok(v.fan<=.65&&v.light<=.5);
  assert.ok(outputLevels({active:true,pulse:1}).light>outputLevels({active:true}).light);
  assert.deepEqual(outputLevels({active:true,scene:'geometry'}).rgb,[150,181,255]);
  assert.equal(encodeOutput(4,{fan:2,light:-1,rgb:[256,NaN,-5]}),'EOL2 SET 4 255 0 255 0 0 800\n');
});
function mockSerial(){
  const packets=[];let closes=0;
  const writer={write:async bytes=>{packets.push(new TextDecoder().decode(bytes));},releaseLock(){}};
  const port={open:async()=>{},writable:{getWriter:()=>writer},close:async()=>{closes++;}};
  return {packets,writer,port,serial:{requestPort:async()=>port,addEventListener(){}},get closes(){return closes;}};
}
test('serial defaults unarmed, throttles, and sends zero on disconnect',async()=>{
  const mock=mockSerial(),s=new SerialOutput(mock.serial);await s.connect();
  s.send({fan:1,light:1,rgb:[255,255,255]},0);await s.pending;assert.match(mock.packets[0],/ 0 0 0 0 0 800/);
  s.setArmed(true);s.send({fan:1,light:1,rgb:[255,255,255]},20);assert.equal(mock.packets.length,1);
  s.send({fan:1,light:1,rgb:[255,255,255]},100);await s.pending;assert.match(mock.packets[1],/ 255 255 255 255 255 800/);
  await s.disconnect();assert.match(mock.packets.at(-1),/ 0 0 0 0 0 800/);assert.equal(mock.closes,1);assert.equal(s.connected,false);
});
test('slow serial writes never build a queue; disarm is reflected by next frame',async()=>{
  const mock=mockSerial(),s=new SerialOutput(mock.serial);await s.connect();s.setArmed(true);
  let resolve;mock.writer.write=()=>new Promise(r=>{resolve=r;});
  s.send({fan:1,light:1},0);await Promise.resolve();const pending=s.pending;
  s.send({fan:1,light:1},1000);assert.equal(s.pending,pending);s.setArmed(false);resolve();await pending;
  mock.writer.write=async bytes=>mock.packets.push(new TextDecoder().decode(bytes));
  s.send({fan:1,light:1},2000);await s.pending;assert.match(mock.packets.at(-1),/ 0 0 0 0 0 800/);await s.disconnect();
});
