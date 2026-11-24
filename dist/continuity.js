/* ARTISTIC RATIONALE
   The same gesture means care in the forest, cooperation in the ruins, and
   reply in the stars. A single drifting character carries those relationships
   forward while the rules give the world a memory of the player's actions. */
import {SpiritSimulation,clamp} from './core.js';
export const COLOR_NAMES=['同相 +','反相 −'];
export const band=v=>v<.12?'安静':v<=.45?'轻声':v<=.85?'舒适发声':'过强';
export const CHAPTERS=[
 {title:'沉睡森林',subtitle:'照顾一群会害怕的光。选一条路，并承担选择的后果。'},
 {title:'回声之门',subtitle:'过去的你守住一侧，现在的你补上另一侧。'},
 {title:'星海回响',subtitle:'看见来波的意图，选择抵消风暴，或增强星光。'},
];
const WAVES=[
 [{kind:'storm',amp:.45},{kind:'beacon',amp:.55}],
 [{kind:'beacon',amp:.35},{kind:'storm',amp:.7},{kind:'beacon',amp:.7}],
 [{kind:'storm',amp:.3},{kind:'beacon',amp:.6}],
];
const REPLAY_GRACE=.45;
const REPLAY_REQUIRED=.85;
const WAVE_TOLERANCE=.16;
export class ContinuitySimulation extends SpiritSimulation{
 reset(chapter=0){
  super.reset(chapter,['gather','gates','resonance'][chapter]);this.adventure=true;this.goal=3;
  this.hand='unknown';this.holding=false;this.voice=0;this.color=0;this.route=0;
  this.round=0;this.tutorial=chapter===0?0:null;this.checkpointRevision=0;
  this.learn={open:0,closed:0,soft:0,full:0,clicked:false};this.feedback='';this.finishText='';
  this.memoryLights=[];this.history=[];this.recording=[];this.userPhrase=[];this.objects=[];
  this.flock=0;this.progress=0;this.trust=1;this.lastClick=-100;this.pending=null;
  this.phase='gather';this.phaseTime=0;this.slot=0;this.slotTime=0;this.slotGood=0;
  this.hits=0;this.waveIndex=0;this.calibrated=false;
 }
 configureChallenge(enabled){this.challenge=enabled;this.gates=[];this.targets=[];if(enabled)this.setup();}
 say(text){this.feedback=text;this.feedbackAt=this.time;}
 get quota(){return 3+this.round;}
 get quiet(){return this.voice>=.12&&this.voice<=.45;}
 get balanced(){return this.voice>=.18&&this.voice<=.8;}
 get slotLength(){return 2;}
 setup(){
  this.phaseTime=0;this.progress=0;this.pending=null;this.y=.52;this.vy=0;this.objects=[];this.hits=0;
  if(this.tutorial!==null){this.phase='learn';this.say('先张开手掌，让小人认出你；再握拳，光会收回掌心。');return;}
  if(this.levelIndex===0){this.phase='gather';this.flock=0;this.trust=1;this.route=0;this.say('张手，用轻声邀请萤火。太响会让它们退开；握拳可以让它们留在身边。');}
  if(this.levelIndex===1){this.phase='record-ready';this.recording=[];this.slot=0;this.slotTime=0;this.slotGood=0;this.say('点击开始记录。每个光印亮起时，保持握拳或张手；至少留下两种动作。');}
  if(this.levelIndex===2){this.waveIndex=0;this.spawnWave();}
 }
 setHand(value){
  if(!['open','closed'].includes(value)||value===this.hand)return false;
  if(value==='closed'&&this.hand==='unknown')return false;
  if(this.complete){this.hand=value;this.holding=value==='closed';return true;}
  const before=this.hand;this.hand=value;this.holding=value==='closed';this.started=true;
  if(value==='closed'){this.heldFor=0;this.charge=0;}
  if(before==='closed'&&value==='open')this.emitGesture();
  return true;
 }
 cancelInput(){this.hand='unknown';this.holding=false;this.heldFor=0;this.charge=0;this.protection=0;}
 cancelHold(){this.cancelInput();}
 // No keyboard or pointer action calls these gesture transitions.
 hold(){return this.setHand('closed');} release(){return this.setHand('open');}
 click(){
  if(this.time-this.lastClick<.2||this.hand==='unknown')return false;
  if(this.complete&&this.levelIndex===CHAPTERS.length-1)return false;
  this.lastClick=this.time;this.count++;
  if(this.complete)return 'next';
  if(this.tutorial!==null){
   if(this.tutorial===2){this.route=1-this.route;this.learn.clicked=true;this.say('路口已经换边。握拳再张手，确认你想走的路。');}
   else this.say('点击收到了。先完成眼前的手势与声音练习，前方才会出现选择。');
   return true;
  }
  if(this.levelIndex===0){
   if(this.phase==='fork'){this.route=1-this.route;this.say(this.route?'已选风带捷径：更快并多带回一束光；风到来时必须握拳。':'已选林荫绕路：保持张手、轻声引导，路更长但没有风带。');}
   else this.say('当前没有岔路。'+this.objective);
  }
  if(this.levelIndex===1){
   if(this.phase==='record-ready'||this.phase==='record'){this.recording=[];this.slot=0;this.slotTime=0;this.phase='record';this.say('正在记录：跟随光印，每两秒留下一个动作。至少一次张手、一次握拳。');}
   else if(['replay-ready','failed'].includes(this.phase)){
    this.phase='replay';this.slot=0;this.goodSlots=[];this.resetReplaySlot();
    this.say('回声开始守门。它握拳时你张手，它张手时你握拳。轻轻哼声维持光桥。');
   }
  }
  if(this.levelIndex===2){this.color=1-this.color;this.say(this.color?'已选反相：你的波谷抵消风暴的波峰。':'已选同相：你的波峰与星光叠加。');}
  return true;
 }
 power(){return this.click();}
 emitGesture(){
  this.count++;this.glow=1;this.lastPower=this.time;this.waves.push({age:0,radius:0,x:.5,y:this.y});
  if(this.tutorial!==null){
   if(this.tutorial===0&&this.learn.open>=.5&&this.learn.closed>=.5){this.tutorial=1;this.checkpointRevision++;this.say('我认得你的收与放了。张手轻声、握拳舒适发声，看看光圈怎样改变。');}
   else if(this.tutorial===1&&this.learn.soft>=.7&&this.learn.full>=.7){this.tutorial=2;this.checkpointRevision++;this.say('我记住了你的声音范围。按一次空格模拟点击，选路，再握拳张手确认。');}
   else if(this.tutorial===2&&this.learn.clicked){
    const selectedRoute=this.route;
    this.tutorial=null;this.checkpointRevision++;this.setup();
    this.route=selectedRoute;
   }
   return;
  }
  if(this.levelIndex===0&&this.phase==='fork'){
   this.phase='travel';this.phaseTime=0;this.progress=0;this.windHit=new Set();this.lossTime=0;
   this.say(this.route?'风带捷径：注意前方卷起的白线，靠近时握拳保护。':'林荫绕路：保持张手和轻声，让萤火跟着你经过花丛。');
  }
  if(this.levelIndex===2&&!this.pending&&this.phase==='wave'){
   if(this.heldFor<.25)return this.say('光还未收稳。握拳至少片刻，观察合成预览，再张手。');
   this.pending={x:.5,amp:this.charge,sign:this.color?-1:1};
   this.say('回应已发出。两道光波相遇时，世界会显示合成结果。');
  }
  if(this.levelIndex===2&&this.phase==='compose'){
   if(this.heldFor<.25)return;
   this.userPhrase.push({amp:this.charge,sign:this.color?-1:1});
   if(this.userPhrase.length===3){this.phase='answer';this.phaseTime=0;this.say('轮到世界回应：它会为你刚才的三道波补上反相波，平息最后的风暴。');}
   else this.say('世界收到了第 '+this.userPhrase.length+' 道波。再自由发出 '+(3-this.userPhrase.length)+' 道波。');
  }
 }
 spawnWave(){
  const spec=WAVES[this.round]?.[this.waveIndex];
  if(!spec){
   if(this.round===2){this.phase='compose';this.userPhrase=[];this.objects=[];this.say('风暴平息了。现在由你发起三道波：选相位、握拳用声音塑形、张手发出。');}
   else this.winRound('你改变了两道光相遇时的结果。');
   return;
  }
  this.phase='wave';this.phaseTime=0;this.pending=null;this.lastResult=null;this.objects=[{...spec,x:1.12,y:.52}];
  this.say(spec.kind==='storm'?'风暴正在靠近：选反相，让预览落在「平静」区域，再张手抵消。':'星灯能量不足：选同相，让预览落在「点亮」区域，再张手增强。');
 }
 get resultant(){const w=this.objects[0];return w?w.amp+(this.color?-1:1)*(this.charge||0):0;}
 get waveTarget(){return this.objects[0]?.kind==='storm'?0:1;}
 get waveTolerance(){return WAVE_TOLERANCE;}
 get waveReady(){return Boolean(this.objects[0])&&Math.abs(this.resultant-this.waveTarget)<=WAVE_TOLERANCE;}
 update(dt,enabled=true){
  if(this.complete||!enabled||this.hand==='unknown')return;
  dt=clamp(dt,0,.04);this.time+=dt;this.elapsed+=dt;this.phaseTime+=dt;this.started=true;
  this.voice=clamp(Number.isFinite(this.controlVoice)?this.controlVoice:0,0,1);
  this.glow=Math.max(0,this.glow-dt);this.hit=Math.max(0,this.hit-dt);this.protection=this.holding?1:0;
  if(this.holding){this.heldFor=(this.heldFor||0)+dt;this.charge+=(this.voice-(this.charge||0))*(1-Math.exp(-dt/.16));}
  const oldY=this.y;this.y+=(.52+Math.sin(this.distance*4)*.045-this.y)*dt*2;this.vy=(this.y-oldY)/Math.max(dt,.001);
  let speed=.018;
  if(this.phase==='travel')speed=this.route?.15:.09;
  if(this.phase==='cross')speed=.18;
  this.distance+=speed*dt;this.speedMultiplier=speed/this.level.speed;this.soundEnergy=this.voice;
  this.trail.forEach(p=>{p.x-=speed*dt;p.life-=dt/7;});this.trail=this.trail.filter(p=>p.life>0).slice(-420);
  this.traceClock+=dt;if(this.traceClock>=1/60){this.traceClock=0;this.trail.push({x:.5,y:this.y,life:1,energy:this.voice});}
  this.waves=this.waves.filter(w=>{w.age+=dt;w.radius=.025+w.age*.38;return w.age<2.6;});
  if(this.tutorial!==null){this.updateLesson(dt);return;}
  if(this.levelIndex===0)this.updateForest(dt);
  if(this.levelIndex===1)this.updateRuins(dt);
  if(this.levelIndex===2)this.updateStars(dt);
 }
 updateLesson(dt){
  if(this.tutorial===0){if(this.hand==='open')this.learn.open+=dt;if(this.holding)this.learn.closed+=dt;}
  if(this.tutorial===1){if(this.hand==='open'&&this.quiet)this.learn.soft+=dt;if(this.holding&&this.voice>=.55&&this.voice<=.9)this.learn.full+=dt;}
 }
 updateForest(dt){
  if(this.phase==='gather'){
   if(this.hand==='open'&&this.quiet){this.progress+=dt*.75;this.trust=Math.min(1,this.trust+dt*.2);}
   else if(this.voice>.7){this.progress=Math.max(0,this.progress-dt);this.trust=Math.max(0,this.trust-dt*.3);}
   if(this.progress>=1){this.progress=0;this.flock++;this.glow=1;this.say('萤火 '+this.flock+'/'+this.quota+' 已靠近。轻声让它们安心。');}
   if(this.flock>=this.quota){this.phase='fork';this.say('前方分岔：林荫绕路更稳，风带捷径更快且多一束光。点击选路，握拳张手确认。');}
  }else if(this.phase==='travel'){
   const duration=this.route?6:10;this.progress=Math.min(1,this.phaseTime/duration);
   if(this.route){
    for(let i=0;i<=this.round;i++){
     const at=(i+1)/(this.round+2);
     if(this.progress>=at&&!this.windHit.has(i)){
      this.windHit.add(i);if(!this.holding){this.flock=Math.max(0,this.flock-1);this.hit=1;this.say('风吹散了一束光。下个风带靠近时，握拳把剩下的光护住。');}else{this.glow=1;this.say('这一阵风被掌心挡住了，萤火仍在。');}
     }
    }
   }else{
    if(this.hand==='open'&&this.quiet)this.lossTime=0;
    else{this.lossTime+=dt;if(this.lossTime>=2){this.flock=Math.max(0,this.flock-1);this.lossTime=0;this.hit=1;this.say('队伍跟丢了：林荫路需要张手、轻声引导。');}}
   }
   if(this.progress>=1){
    if(this.route&&this.flock>=this.quota)this.flock++;
    this.phase='nest';this.progress=0;
    this.say(this.flock>=this.quota?'到家了。张手，轻声让萤火落进花蕾。':'张手轻声召回散失的萤火。凑齐后，同一朵花蕾会接住它们。');
   }
  }else if(this.phase==='nest'){
   if(this.hand==='open'&&this.quiet)this.progress+=dt;else this.progress=Math.max(0,this.progress-dt*.5);
   if(this.progress>=1.2){
    if(this.flock<this.quota){this.flock++;this.progress=0;this.say('一束散失的萤火找回来了。继续轻声等待。');}
    else{this.score+=Math.max(0,this.flock-this.quota)*75;this.winRound('你带回了 '+this.flock+' 束光，花蕾为你让开道路。');}
   }
  }
 }
 resetReplaySlot(){
  this.slotTime=0;this.slotGood=0;this.slotHandGood=0;this.slotVoiceGood=0;
 }
 get replayRequired(){return REPLAY_REQUIRED;}
 updateRuins(dt){
  if(this.phase==='record'){
   this.slotTime+=dt;
   if(this.slotTime>=this.slotLength){
    this.recording.push(this.hand);this.slotTime=0;this.slot++;
    if(this.slot===this.round+2){
     if(new Set(this.recording).size<2){this.phase='record-ready';this.say('回声只有一种动作，无法分工。点击重录，至少包含一次握拳和一次张手。');}
     else{this.phase='replay-ready';this.say('回声记住了你的 '+this.recording.length+' 个动作。点击开始合作：你做相反动作，用舒适声音维持光桥。');}
    }
   }
  }else if(this.phase==='replay'){
   this.slotTime+=dt;
   const expected=this.recording[this.slot]==='closed'?'open':'closed';
   const scorable=Math.min(dt,Math.max(0,this.slotTime-REPLAY_GRACE));
   const handMatches=this.hand===expected;
   if(handMatches)this.slotHandGood+=scorable;
   if(this.balanced)this.slotVoiceGood+=scorable;
   if(handMatches&&this.balanced)this.slotGood+=scorable;
   if(this.slotTime>=this.slotLength){
    if(this.slotGood<REPLAY_REQUIRED){
     const reasons=[];
     if(this.slotHandGood<REPLAY_REQUIRED)reasons.push('相反动作保持时间不足');
     if(this.slotVoiceGood<REPLAY_REQUIRED)reasons.push('声音在亮区停留不足');
     if(!reasons.length)reasons.push('动作和声音同时到位的时间不足');
     this.phase='failed';this.missed++;
     this.say('第 '+(this.slot+1)+' 个光印没接稳：'+reasons.join('，')+'。记录还在，点击再合作一次。');
    }
    else{this.goodSlots.push(true);this.slot++;this.resetReplaySlot();this.glow=1;
     if(this.slot===this.recording.length){this.phase='cross';this.phaseTime=0;this.say('两条光路同时亮起。你与过去的自己一起穿过门。');}
    }
   }
  }else if(this.phase==='cross'&&this.phaseTime>2.5)this.winRound('回声替你守住一边，你补全了另一边。');
 }
 updateStars(dt){
  if(this.phase==='wave'){
   const w=this.objects[0];w.x-=dt*(this.holding?.035:.11);
   if(this.pending){
    this.pending.x+=dt*.34;
    if(this.pending.x>=w.x){
     const result=w.amp+this.pending.sign*this.pending.amp,good=Math.abs(result-this.waveTarget)<=WAVE_TOLERANCE;
     this.lastResult={...w,result,good};this.pending=null;
     if(good){this.glow=1;this.waveIndex++;this.phase='resolved';this.phaseTime=0;this.say(w.kind==='storm'?'波峰与波谷抵消，风暴平静了。':'两道波相加，星灯得到恰好的光。');}
     else{this.hit=1;this.missed++;this.phase='wave-retry';this.phaseTime=0;this.say('合成 '+result.toFixed(2)+'。'+(w.kind==='storm'?'还有残余风浪。':'星灯没有达到亮区。')+'调整相位与声音，再试同一道波。');}
    }
   }
   if(this.phase==='wave'&&w.x<=.5){this.hit=1;this.missed++;this.phase='wave-retry';this.phaseTime=0;this.pending=null;this.say('来波经过了小人。握拳会放慢它，给你观察合成预览的时间。');}
  }else if(['resolved','wave-retry'].includes(this.phase)&&this.phaseTime>1.6)this.spawnWave();
  else if(this.phase==='answer'&&this.phaseTime>6)this.winRound('这一次世界学会了回应你：它用相反的波，把你的每一次发声接稳。');
 }
 winRound(text){
  this.passed++;this.round++;this.score+=300;this.checkpointRevision++;this.glow=1;
  if(this.round===3){this.complete=true;this.finishText=text;this.say(text);return;}
  this.setup();this.say(text+' '+this.feedback);
 }
 retry(){
  if(this.complete)return;
  this.cancelInput();
  if(this.tutorial===0){this.learn.open=0;this.learn.closed=0;}
  if(this.tutorial===1){this.learn.soft=0;this.learn.full=0;}
  if(this.tutorial===2){this.learn.clicked=false;this.route=0;}
  this.setup();
  this.say('当前小段重新开始，已完成的路保留。先张开手掌。');
 }
 save(){return {version:2,chapter:this.levelIndex,round:this.round,tutorial:this.tutorial,score:this.score,phrase:this.userPhrase};}
 restore(d){
  if(!d||d.version!==2||!Number.isInteger(d.chapter)||d.chapter<0||d.chapter>2||!Number.isInteger(d.round)||d.round<0||d.round>3||![null,0,1,2].includes(d.tutorial)||!Number.isFinite(d.score)||d.score<0||(d.tutorial!==null&&(d.chapter!==0||d.round!==0)))return false;
  this.reset(d.chapter);this.tutorial=d.tutorial;this.round=this.passed=d.round;this.score=d.score;this.challenge=true;
  if(d.round===3){this.complete=true;this.finishText='世界保留了这段共同完成的旅程。';}else this.setup();
  if(d.chapter===2&&Array.isArray(d.phrase)&&d.phrase.length<=3&&d.phrase.every(p=>p&&[1,-1].includes(p.sign)&&Number.isFinite(p.amp)&&p.amp>=0&&p.amp<=1)){
   this.userPhrase=d.phrase.map(({amp,sign})=>({amp,sign}));
  }
  return true;
 }
 get objective(){
  if(this.tutorial!==null)return ['庭院：张手认识 → 握拳收光 → 张手释放','庭院：张手轻声 + 握拳舒适发声，再张手','庭院：空格模拟点击选路，握拳张手确认'][this.tutorial];
  if(this.levelIndex===0)return '护送 '+(this.round+1)+'/3 · '+({gather:'张手轻声，邀请 '+this.quota+' 束萤火',fork:'点击选择路线，握拳张手出发',travel:this.route?'风带靠近时握拳护光':'张手轻声，引导队伍走林荫路',nest:'张手轻声，让萤火入巢'}[this.phase]);
  if(this.levelIndex===1)return '双声门 '+(this.round+1)+'/3 · '+({'record-ready':'点击开始留下动作',record:'保持一个动作，光印亮满时记录','replay-ready':'点击，让你的回声开始守门',replay:'做回声的相反动作，并维持中间声音',failed:'记录保留，点击重试合作',cross:'双通道已打开，正在穿行'}[this.phase]);
  return '星海 '+(this.round+1)+'/3 · '+({wave:this.objects[0]?.kind==='storm'?'反相抵消风暴':'同相增强星灯',resolved:'合成成功，下一道波正在接近','wave-retry':'这一道波会再来',compose:'自由发出三道波，让世界回应',answer:'世界正在补上你的反相波'}[this.phase]);
 }
 get cue(){
  if(this.tutorial===0)return '张手 '+(this.learn.open>=.5?'✓':'○')+'　握拳 '+(this.learn.closed>=.5?'✓':'○');
  if(this.tutorial===1)return '张手轻声 '+(this.learn.soft>=.7?'✓':'○')+'　握拳舒适发声 '+(this.learn.full>=.7?'✓':'○');
  if(this.tutorial===2)return (this.route?'风带捷径':'林荫绕路')+' · '+(this.learn.clicked?'已选择':'等待点击');
  if(this.levelIndex===0)return '同行萤火 '+this.flock+'/'+this.quota+' · '+(this.phase==='travel'?'路程 '+Math.round(this.progress*100)+'%':this.quiet?'声音温和':this.voice<.12?'试着轻轻哼声':'声音偏强，萤火会退开');
  if(this.levelIndex===1){
   if(['record','replay'].includes(this.phase))return '第 '+(this.slot+1)+' / '+(this.round+2)+' 印 · '+(this.slotLength-this.slotTime).toFixed(1)+' 秒'+(this.phase==='replay'?' · 回声'+(this.recording[this.slot]==='closed'?'握拳 → 你张手':'张手 → 你握拳'):' · 当前'+(this.holding?'握拳':'张手'));
   return '声音亮区：18%–80% · 点击开始或重试';
  }
  return this.phase==='wave'?(this.color?'反相 −':'同相 +')+' · '+(this.pending?'光波正在相遇':this.holding?(this.waveReady?'合成预览已进入目标区，张手':'调声音，让合成预览进入目标区'):'握拳查看合成预览'):'玩家已发出 '+this.userPhrase.length+' / 3 道波';
 }
}
