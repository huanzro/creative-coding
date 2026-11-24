import {AtmosphereMotion,atmosphereAt} from '../dist/atmosphere.js';
import test from 'node:test';
import assert from 'node:assert/strict';
import {GestureGate, SpiritSimulation, handOpenness, LEVELS, worldToScreen} from '../dist/core.js';
import {rankRuns,saveRun,loadRuns,boardRuns} from '../dist/leaderboard.js';

test('one stable open-close cycle produces one lift; a held fist never repeats',()=>{
  const g=new GestureGate();assert.equal(g.update(1,0),false);assert.equal(g.update(1,200),false);
  assert.equal(g.update(2,300),false);assert.equal(g.update(2,500),false);
  assert.equal(g.update(1,600),false);assert.equal(g.update(1,800),true);
  for(let t=900;t<3000;t+=100)assert.equal(g.update(1,t),false);
  g.update(2,3100);g.update(2,3300);g.update(1,3400);assert.equal(g.update(1,3600),true);
});
test('flicker, hand loss, and reset cannot trigger a stale armed gesture',()=>{
  const g=new GestureGate();g.update(2,0);g.update(2,200);g.update(1,230);g.update(2,260);assert.equal(g.update(1,290),false);
  g.update(null,300);g.update(1,500);assert.equal(g.update(1,700),false);
  g.update(2,800);g.update(2,1000);g.reset();g.update(1,1100);assert.equal(g.update(1,1400),false);
});
test('physics is bounded and reset clears all session state',()=>{
  const s=new SpiritSimulation();assert.equal(s.lift(),true);assert.equal(s.lift(),false);
  const start=s.y;for(let i=0;i<10;i++)s.update(1/60);assert.ok(s.y<start);
  for(let i=0;i<12000;i++){if(i%31===0)s.lift();s.update(1/60,true);assert.ok(s.y>=.16&&s.y<=.82);}
  assert.ok(s.trail.length<=450);assert.ok(s.particles.length<=200);assert.ok(s.gates.length<=6);
  s.reset();assert.equal(s.count,0);assert.equal(s.started,false);assert.equal(s.trail.length,0);assert.equal(s.gates.length,0);assert.equal(s.y,.52);
});
test('missing or malformed hand data cannot become an accepted score',()=>{
  assert.equal(handOpenness(null),null);assert.equal(handOpenness([]),null);
  assert.equal(handOpenness(Array.from({length:21},()=>({x:NaN,y:0}))),null);
});

test('camera projection keeps the spirit at center while a fixed world point moves oppositely',()=>{
  for(const y of [.16,.5,.82])assert.deepEqual(worldToScreen(.5,y,y),{x:.5,y:.5});
  assert.ok(worldToScreen(.8,.5,.3).y>worldToScreen(.8,.5,.6).y);
});
test('collision retries the same gate without awarding a pass',()=>{
  const s=new SpiritSimulation();s.configureChallenge(true);s.started=true;s.y=.8;s.gates[0].x=.5;s.gates[0].y=.3;s.gates[0].baseY=.3;
  s.update(1/60,true);assert.equal(s.passed,0);assert.equal(s.collisions,1);assert.equal(s.gates[0].passed,false);assert.ok(s.gates[0].x>.8);assert.ok(s.recovery>0);
});
test('all three levels are traversable, finish, score, and remain finished',()=>{
  for(let index=0;index<LEVELS.length;index++){
    const s=new SpiritSimulation();s.reset(index);s.configureChallenge(true);s.lift();
    for(let i=0;i<60*100&&!s.complete;i++){
      const target=s.gates.find(g=>!g.passed)?.y??.5;
      if(s.y>target+.025&&s.vy>=0)s.lift();s.update(1/60,true);
    }
    assert.equal(s.complete,true,'level '+index+' is reachable');assert.equal(s.passed,LEVELS[index].heights.length);assert.ok(s.score>0);
    const time=s.elapsed,count=s.count;s.update(1,true);assert.equal(s.lift(),false);assert.equal(s.elapsed,time);assert.equal(s.count,count);
  }
});
test('rankings respond to score changes, preserve per-run uniqueness, and handle unavailable storage',()=>{
  let raw=null;const storage={getItem:()=>raw,setItem:(_,v)=>raw=v};
  const a={id:'a',name:'旅人 01',level:'forest',score:150,passed:1,elapsed:7};
  const b={...a,id:'b',name:'旅人 02',score:100};
  assert.equal(rankRuns([a,b])[0].id,'a');b.score=200;assert.equal(rankRuns([a,b])[0].id,'b');
  let result=saveRun(storage,[],a);result=saveRun(storage,result.runs,{...a,score:300});assert.equal(result.runs.length,1);assert.equal(loadRuns(storage)[0].score,300);
  assert.equal(saveRun(undefined,[],a).saved,false);assert.deepEqual(loadRuns({getItem:()=>'{bad'}),[]);
  assert.equal(saveRun(storage,[],{...a,passed:0}).runs.length,0);
});

test('gather and resonance are completable in all backgrounds with no gates',()=>{
  for(let level=0;level<3;level++)for(const mode of ['gather','resonance']){
    const s=new SpiritSimulation();s.reset(level,mode);s.configureChallenge(true);s.lift();
    assert.equal(s.gates.length,0);
    for(let i=0;i<60*180&&!s.complete;i++){
      const target=s.targets.filter(t=>t.active&&t.x>.25).sort((a,b)=>a.x-b.x)[0];
      if(target&&s.y>target.y+.025&&s.vy>=0)s.lift();
      if(target&&target.x<.82)s.power();s.update(1/60,true);
    }
    assert.equal(s.complete,true,level+' '+mode);assert.equal(s.passed,s.goal);assert.ok(s.score>0);assert.ok(s.waves.length<=4);
    const score=s.score;s.power();s.update(.04,true);assert.equal(s.score,score);
  }
});
test('missed lights return without counting as success, and cannot score twice',()=>{
  const s=new SpiritSimulation();s.reset(0,'gather');s.configureChallenge(true);s.started=true;
  s.targets[0].x=.1;s.targets[0].y=.2;s.update(.01,true);assert.equal(s.passed,0);assert.equal(s.missed,1);assert.ok(s.targets[0].x>1);
  s.targets[0].x=.5;s.targets[0].y=s.y;s.update(.01,true);assert.equal(s.passed,1);const score=s.score;s.collect(s.targets[0]);assert.equal(s.score,score);
});
test('power cooldown and reset clear all action state',()=>{
  const s=new SpiritSimulation();s.reset(2,'resonance');s.configureChallenge(true);assert.equal(s.power(),true);assert.equal(s.power(),false);assert.equal(s.count,1);
  s.reset(0,'gather');assert.equal(s.waves.length,0);assert.equal(s.targets.length,0);assert.equal(s.missed,0);assert.equal(s.magnet,0);
});
test('different gameplay modes never mix rankings; legacy scores remain gate scores',()=>{
  const r={id:'old',name:'旅人',level:'forest',score:500,passed:5,elapsed:40};
  const current={...r,id:'new',score:0,mode:'gather'};assert.equal(boardRuns([r],current).length,1);
  current.mode='gates';assert.equal(boardRuns([r],current).length,2);
});
test('time-of-day presets and automatic transitions are deterministic and bounded',()=>{
  assert.equal(atmosphereAt(0).name,'薄暮');assert.equal(atmosphereAt(1).name,'破晓');assert.equal(atmosphereAt(.2,'night').name,'月夜');
  const a=atmosphereAt(.3332),b=atmosphereAt(.3334);assert.ok(Math.abs(a.strength-b.strength)<.001);
  for(let p=0;p<1;p+=.01){const v=atmosphereAt(p);assert.ok(v.warm.every(c=>c>=0&&c<=255));}
});
test('background flow responds to voice and hand without changing game progress',()=>{
  const quiet=new AtmosphereMotion(),voice=new AtmosphereMotion(),fist=new AtmosphereMotion();
  const openState=Object.freeze({voice:.7,holding:false,time:5,elapsed:5});
  const heldState={...openState,holding:true};
  for(let i=0;i<60;i++){
    quiet.update(1/60,{...openState,voice:0},true);
    voice.update(1/60,openState,true);
    fist.update(1/60,heldState,true);
  }
  assert.ok(voice.drift>quiet.drift);
  assert.ok(voice.drift>fist.drift);
  assert.ok(voice.wake>fist.wake+.4);
  const distance=voice.drift;
  for(let i=0;i<60;i++)voice.update(1/60,openState,false);
  assert.equal(voice.drift,distance);
  assert.ok(voice.time>1.9);
  assert.equal(openState.elapsed,5);
});

test('restored open-close gesture lifts once and then gravity lowers the spirit',()=>{
  const gate=new GestureGate(),s=new SpiritSimulation();
  for(const [score,time]of [[2,0],[2,200],[1,300],[1,500],[1,900],[1,1500]])if(gate.update(score,time))s.lift();
  assert.equal(s.count,1);assert.ok(s.vy<0);const initial=s.y;
  for(let i=0;i<20;i++)s.update(1/60,false);assert.ok(s.y<initial);
  for(let i=0;i<100;i++)s.update(1/60,false);assert.ok(s.y>initial);
});
test('resonance reaches targets by circular screen distance on wide displays',()=>{
  const s=new SpiritSimulation();s.reset(2,'resonance');s.aspect=2;s.configureChallenge(true);s.started=true;
  s.targets=[{x:.7,y:s.y,baseY:s.y,index:0,active:true}];s.waves=[{x:.5,y:s.y,age:0,radius:0}];
  s.updateTargets(.5,0);assert.equal(s.passed,0);s.updateTargets(.5,0);assert.equal(s.passed,1);assert.ok(s.targets[0].echo>0);
});
