export const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

// Scale-invariant finger extension: four fingertip/wrist distances divided
// by their corresponding knuckle/wrist distances. Thumb is intentionally ignored.
export function handOpenness(points) {
  if (!points || points.length !== 21) return null;
  const distance = (a, b) => Math.hypot(a.x-b.x, a.y-b.y, (a.z||0)-(b.z||0));
  const values = [8,12,16,20].map(tip => distance(points[tip], points[0]) / Math.max(.001, distance(points[tip-3],points[0])));
  const score = values.reduce((a,b) => a+b,0)/4;
  return Number.isFinite(score) ? score : null;
}

export class GestureGate {
  constructor() { this.openThreshold=1.65; this.closedThreshold=1.25; this.reset(); }
  reset() { this.candidate=null; this.since=0; this.state='unknown'; this.armed=false; this.lastTrigger=-Infinity; }
  update(score, now) {
    if (score === null) { this.reset(); return false; }
    const state=score >= this.openThreshold ? 'open' : score <= this.closedThreshold ? 'closed' : 'between';
    if (state !== this.candidate) { this.candidate=state; this.since=now; }
    if (now-this.since < 140 || state==='between') return false;
    this.state=state;
    if (state==='open') this.armed=true;
    if (state==='closed' && this.armed && now-this.lastTrigger>380) {
      this.armed=false; this.lastTrigger=now; return true;
    }
    return false;
  }
}

export const PLAY_MODES={gates:{name:'穿行',goal:5},gather:{name:'寻光',goal:8},resonance:{name:'共鸣',goal:6}};
export const LEVELS = [
  {id:'forest',name:'沉睡森林',subtitle:'穿过五道树门，让沉睡的花依次亮起。',speed:.085,gap:.48,heights:[.54,.43,.57,.39,.50],motion:0},
  {id:'geometry',name:'回声之门',subtitle:'观察光门的起伏，在变化的间隙中穿行。',speed:.095,gap:.42,heights:[.50,.39,.55,.42,.54],motion:.045},
  {id:'stars',name:'星海回响',subtitle:'穿过六道星环，把孤独的光连成星座。',speed:.105,gap:.37,heights:[.52,.39,.55,.40,.53,.45],motion:.025},
];
export function worldToScreen(x,y,cameraY){return {x,y:y-cameraY+.5};}
export class SpiritSimulation {
  constructor() { this.reset(); }
  reset(levelIndex=0,playMode='gates') {
    this.levelIndex=clamp(levelIndex,0,LEVELS.length-1);this.level=LEVELS[this.levelIndex];
    this.playMode=Object.hasOwn(PLAY_MODES,playMode)?playMode:'gates';this.goal=this.playMode==='gates'?this.level.heights.length:PLAY_MODES[this.playMode].goal;
    this.targets=[];this.waves=[];this.magnet=0;this.lastPower=-Infinity;this.missed=0;
    this.y=.52;this.vy=0;this.aspect=this.aspect||16/9;this.time=0;this.elapsed=0;this.count=0;this.started=false;
    this.glow=0;this.hit=0;this.protection=0;this.lastLift=-Infinity;this.trail=[];this.particles=[];
    this.gates=[];this.passed=0;this.score=0;this.combo=0;this.collisions=0;this.complete=false;
    this.distance=0;this.recovery=0;this.challenge=false;this.lastAward='';this.awardTime=0;this.lights=[];
  }
  configureChallenge(enabled) {
    this.challenge=enabled;
    this.targets=enabled&&this.playMode!=='gates'?Array.from({length:this.goal},(_,i)=>({x:1.02+i*.32,y:[.54,.43,.60,.38,.53,.44,.58,.48][i%8],baseY:[.54,.43,.60,.38,.53,.44,.58,.48][i%8],index:i,active:true})):[];
    this.gates=enabled&&this.playMode==='gates'?this.level.heights.map((y,i)=>({x:1.08+i*.46,baseY:y,y,gap:this.level.gap,index:i,passed:false,attempts:0})):[];
  }
  lift() {
    if(this.complete||this.recovery>0||this.time-this.lastLift<.14)return false;
    this.lastLift=this.time;this.started=true;
    if(this.playMode!=='gates'&&this.challenge)this.power(true);this.vy=-.32;this.glow=1;this.count++;
    for(let i=0;i<22;i++)this.particles.push({x:.5,y:this.y,vx:(Math.random()-.65)*.15,vy:(Math.random()-.5)*.16,life:1,size:Math.random()*2+1});
    return true;
  }
  power(fromLift=false){
    if(this.complete||this.recovery>0||this.playMode==='gates'||!this.challenge||this.time-this.lastPower<.85)return false;
    this.started=true;this.lastPower=this.time;this.glow=1;if(!fromLift)this.count++;
    if(this.playMode==='gather')this.magnet=1.2;
    else this.waves.push({x:.5,y:this.y,age:0,radius:0});
    return true;
  }
  collect(target,precise=false){
    if(!target.active)return;target.active=false;target.echo=1.4;this.passed++;this.combo++;
    const award=(this.playMode==='gather'?80:120)+Math.min(this.combo-1,4)*15+(precise?30:0);
    this.score+=award;this.glow=1;this.awardTime=this.time;
    this.lastAward=(this.playMode==='gather'?'萤火归队':'星光回应')+' +'+award;
    this.lights.push({x:target.x,y:target.y,index:target.index});
  }
  updateTargets(dt,scroll){
    this.magnet=Math.max(0,this.magnet-dt);
    this.waves.forEach(w=>{w.age+=dt;w.x=.5;w.y=this.y;w.radius=.025+w.age*.38;});this.waves=this.waves.filter(w=>w.age<2.6);
    for(const target of this.targets){
      target.echo=Math.max(0,(target.echo||0)-dt);target.x-=scroll;if(!target.active)continue;
      if(this.playMode==='gather'){
        const dist=Math.hypot(target.x-.5,target.y-this.y);
        if(this.magnet>0&&dist<.23){target.x+=(.5-target.x)*dt*3;target.y+=(this.y-target.y)*dt*3;}
        if(Math.hypot(target.x-.5,target.y-this.y)<.075)this.collect(target,dist<.045);
      }else{
        target.y=target.baseY+Math.sin(this.elapsed*.65+target.index)*.025;
        for(const w of this.waves){const d=Math.hypot((target.x-w.x)*this.aspect,target.y-w.y);if(Math.abs(d-w.radius)<.035){this.collect(target,Math.abs(target.y-this.y)<.08);break;}}
      }
      if(target.active&&target.x<.18){
        this.missed++;this.combo=0;this.score=Math.max(0,this.score-10);
        target.x=Math.max(1.05,...this.targets.filter(t=>t.active&&t!==target).map(t=>t.x+.30));
        this.lastAward='这束光会再回来 · −10';this.awardTime=this.time;
      }
    }
    if(this.passed===this.goal){this.complete=true;this.vy=0;this.lastAward='这一片世界，已经被你点亮。';}
  }
  update(dt,obstacles=false) {
    if(this.complete)return;
    if(obstacles!==this.challenge)this.configureChallenge(obstacles);
    dt=clamp(dt,0,.04);this.time+=dt;this.glow=Math.max(0,this.glow-dt*1.5);this.hit=Math.max(0,this.hit-dt);this.protection=Math.max(0,this.protection-dt);
    if(this.recovery>0){this.recovery=Math.max(0,this.recovery-dt);return;}
    if(this.started){this.elapsed+=dt;this.vy+=.40*dt;this.y+=this.vy*dt;if(this.y>.82){this.y=.82;this.vy=0;}if(this.y<.16){this.y=.16;this.vy=Math.max(0,this.vy);}}
    const scroll=this.started?this.level.speed*dt:0;this.distance+=scroll;
    this.trail.forEach(p=>{p.x-=scroll;p.life-=dt/7;});this.trail=this.trail.filter(p=>p.life>0&&p.x>-.1);
    if(this.started)this.trail.push({x:.5,y:this.y,life:1});if(this.trail.length>450)this.trail.shift();
    this.particles.forEach(p=>{p.x+=p.vx*dt-scroll;p.y+=p.vy*dt;p.vy+=dt*.05;p.life-=dt*.7;});this.particles=this.particles.filter(p=>p.life>0).slice(-200);
    if(!this.started||!this.challenge)return;
    if(this.playMode!=='gates'){this.updateTargets(dt,scroll);return;}
    for(const gate of this.gates){gate.x-=scroll;gate.y=gate.baseY+Math.sin(this.elapsed*.85+gate.index*1.2)*this.level.motion;}
    for(const gate of this.gates){
      if(gate.passed)continue;
      // Radius includes the full head and feet; render and collision share gate.gap.
      if(Math.abs(gate.x-.5)<.043&&Math.abs(this.y-gate.y)+.065>gate.gap/2){
        this.hit=1;this.glow=1;this.collisions++;this.combo=0;gate.attempts++;
        this.score=Math.max(0,this.score-20);this.lastAward='轻碰 · −20 · 从这道门再试';this.awardTime=this.time;
        const shift=.88-gate.x;for(const g of this.gates)if(!g.passed)g.x+=shift;
        this.y=gate.y;this.vy=0;this.recovery=.65;this.protection=1.2;this.trail=[];return;
      }
      if(gate.x<.45){
        gate.passed=true;this.passed++;this.combo++;
        const precise=Math.abs(this.y-gate.y)<.075,award=100+Math.min(this.combo-1,4)*20+(precise?50:0);
        this.score+=award;this.glow=1;this.lastAward=(precise?'精准穿行':'穿行成功')+' +'+award;this.awardTime=this.time;
        this.lights.push({x:gate.x,y:gate.y,index:gate.index});
      }
    }
    this.gates=this.gates.filter(g=>g.x>-.15);
    if(this.passed===this.level.heights.length){this.complete=true;this.vy=0;this.lastAward='这一片世界，已经被你点亮。';}
  }
}
