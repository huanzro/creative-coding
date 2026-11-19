import {SpiritSimulation, GestureGate, handOpenness, clamp, LEVELS, worldToScreen, PLAY_MODES} from './core.js';

import {boardRuns,loadRuns,saveRun} from './leaderboard.js';
import {loadAtmosphere,drawAtmosphere,atmosphereAt} from './atmosphere.js';

const $=id=>document.getElementById(id);
const canvas=$('world'), video=$('webcam'), handCanvas=$('hand-overlay'), hc=handCanvas.getContext('2d');
let ctx=canvas.getContext('2d');
const mainContext=ctx;
let projectionWindow=null, projectionCanvas=null;
let splitProjection=false;
const sim=new SpiritSimulation(), gesture=new GestureGate();
let mode='keyboard', scene='forest', manualPause=false, cameraActive=false, loading=false, stream=null, detector=null;
let handPresent=false, lastHand=0, lastInference=0, lastVideoTime=-1, frameTime=performance.now(), lastUI=0, calibration=null, cameraGeneration=0, inputHeld=false;
let W=1000,H=800,dpr=1;
let rankingStorage;try{rankingStorage=window.localStorage;}catch{}
let runs=loadRuns(rankingStorage),runId=crypto.randomUUID(),runName='旅人 '+String(runs.length+1).padStart(2,'0'),recorded=false,lastRank=null,rankMessageUntil=0;
const rankRows=new Map();
function currentRun(){return {id:runId,name:runName,level:sim.level.id,mode:sim.playMode,score:sim.score,passed:sim.passed,elapsed:Math.round(sim.elapsed*1000)/1000,complete:sim.complete};}
function archiveRun(){if(recorded||!sim.challenge)return;const result=saveRun(rankingStorage,runs,currentRun());runs=result.runs;recorded=true;if(!result.saved)$('ranking-note').textContent='浏览器未允许保存 · 本次排名仍可用';}
function newRun(){runId=crypto.randomUUID();runName='旅人 '+String(runs.length+1).padStart(2,'0');recorded=false;lastRank=null;rankRows.forEach(row=>row.remove());rankRows.clear();}
function updateRanking(){
  const board=boardRuns(runs,currentRun());
  const rank=board.findIndex(r=>r.id===runId)+1;
  if(lastRank!==null&&rank!==lastRank){rankMessageUntil=performance.now()+2500;$('rank-change').textContent=rank<lastRank?'↑ 上升 '+(lastRank-rank)+' 名':'↓ 下降 '+(rank-lastRank)+' 名';}
  lastRank=rank;if(performance.now()>rankMessageUntil)$('rank-change').textContent='你 · 第 '+rank+' 名';
  $('ranking-level').textContent=PLAY_MODES[sim.playMode].name+' · 本机榜';
  const visible=board.slice(0,4);if(!visible.some(r=>r.id===runId))visible.push(currentRun());
  const ids=new Set(visible.map(r=>r.id));for(const [id,row]of rankRows)if(!ids.has(id)){row.remove();rankRows.delete(id);}
  visible.forEach((r,index)=>{let row=rankRows.get(r.id);if(!row){row=document.createElement('li');row.className='rank-row';for(const cls of ['rank-number','rank-name','rank-score']){const span=document.createElement('span');span.className=cls;row.appendChild(span);}$('rank-list').appendChild(row);rankRows.set(r.id,row);}
    row.classList.toggle('current',r.id===runId);row.style.transform='translateY('+index*34+'px)';
    row.children[0].textContent=String(board.findIndex(item=>item.id===r.id)+1).padStart(2,'0');row.children[1].textContent=r.id===runId?'你 · '+(sim.complete?'已完成':'进行中'):r.name;row.children[2].textContent=r.score;
  });$('rank-list').style.height=visible.length*34+'px';
  $('ranking-empty').hidden=board.length>1;$('ranking').hidden=scene==='black'||!sim.challenge;
}
const stars=Array.from({length:100},(_,i)=>({x:random(i*3+1),y:random(i*7+3),r:.5+random(i+8)*1.4,phase:i*.65}));
function random(n){const x=Math.sin(n*127.1+311.7)*43758.5453;return x-Math.floor(x);}
function resize(){const rect=canvas.getBoundingClientRect();W=rect.width;H=rect.height;sim.aspect=W/Math.max(H,1);dpr=Math.min(devicePixelRatio||1,2);canvas.width=Math.round(W*dpr);canvas.height=Math.round(H*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);}
new ResizeObserver(resize).observe(canvas);
function signal(text,type='ready'){if($('signal-text').textContent!==text)$('signal-text').textContent=text;$('signal-dot').style.background=type==='error'?'#d69583':type==='waiting'?'#c3ae7e':'#92bba6';}
function isWaiting(){return mode==='camera'&&(!cameraActive||!handPresent||!!calibration);}
function paused(){return manualPause||isWaiting();}
function lift(source){if(paused()||source!==mode)return false;const accepted=sim.lift();if(accepted){$('count').textContent=String(sim.count).padStart(2,'0');$('response-flash').classList.add('lit');$('activity').textContent='已回应 · 微光向上跃起';setTimeout(()=>$('response-flash').classList.remove('lit'),180);}return accepted;}
function reset(){archiveRun();sim.reset(sim.levelIndex,$('gameplay').value);sim.configureChallenge($('obstacles').checked&&scene!=='black');newRun();gesture.reset();manualPause=false;calibration=null;$('count').textContent='00';$('activity').textContent='按空格出发 · 小人保持在中央';$('pause').innerHTML='暂停 <kbd>P</kbd>';$('calibrate').textContent='校准我的手势';$('chapter-complete').hidden=true;updateModeHints();updateUI();}
function setMode(next){if(next===mode)return;mode=next;gesture.reset();inputHeld=false;calibration=null;manualPause=false;$('pause').innerHTML='暂停 <kbd>P</kbd>';
  for(const id of ['keyboard','camera']){$(id+'-mode').classList.toggle('selected',id===mode);$(id+'-mode').setAttribute('aria-pressed',String(id===mode));$(id+'-help').hidden=id!==mode;}
  $('input-badge').textContent=mode==='keyboard'?'键盘':'手势';$('action-hint').innerHTML=mode==='keyboard'?'<kbd>SPACE</kbd> 轻按一下，唤起微光':'张开手掌 → 握拳，让微光跃起';$('lift').hidden=mode!=='keyboard';
  if(mode==='keyboard'){stopCamera();signal('键盘已就绪');}else signal('点击「开启摄像头」，然后张开手掌','waiting');updateModeHints();
}
function setScene(next){
  archiveRun();scene=next;const index=LEVELS.findIndex(level=>level.id===scene);if(index>=0)sim.levelIndex=index;
  document.body.classList.toggle('black-scene',scene==='black');document.querySelectorAll('[data-scene]').forEach(b=>{b.classList.toggle('selected',b.dataset.scene===scene);b.setAttribute('aria-pressed',String(b.dataset.scene===scene));});
  $('obstacles').disabled=scene==='black';$('obstacles').checked=scene!=='black';$('gameplay').disabled=scene==='black';if(scene!=='black')$('gameplay').value={forest:'gather',geometry:'gates',stars:'resonance'}[scene];
  const label=scene==='black'?'00 / LIGHT STUDY':String(index+1).padStart(2,'0')+' / '+['PRESENCE','RECOGNITION','EXPRESSION'][index];
  $('scene-index').textContent=label;$('scene-title').textContent=scene==='black'?'一束光，一个回应。':LEVELS[index].name;
  $('scene-subtitle').textContent=scene==='black'?'固定中心 · 纯黑角色展示':LEVELS[index].subtitle;reset();
}
function togglePause(){manualPause=!manualPause;gesture.reset();$('pause').innerHTML=manualPause?'继续 <kbd>P</kbd>':'暂停 <kbd>P</kbd>';}
$('keyboard-mode').onclick=()=>setMode('keyboard');$('camera-mode').onclick=()=>setMode('camera');$('pause').onclick=togglePause;$('reset').onclick=reset;$('lift').onclick=()=>lift('keyboard');
document.querySelectorAll('[data-scene]').forEach(b=>b.onclick=()=>setScene(b.dataset.scene));
$('next-level').onclick=()=>setScene(LEVELS[(sim.levelIndex+1)%LEVELS.length].id);
$('obstacles').onchange=reset;$('gameplay').onchange=()=>{reset();$('gameplay').blur();};$('time-of-day').onchange=()=>$('time-of-day').blur();
$('power').onclick=()=>{if(!paused()&&mode==='keyboard')sim.power();};
$('ranking-toggle').onclick=()=>{const collapsed=$('ranking').classList.toggle('collapsed');$('ranking-toggle').setAttribute('aria-expanded',String(!collapsed));};
window.addEventListener('pagehide',archiveRun);
$('immersive').onclick=()=>document.body.classList.toggle('immersive');
$('fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen();}catch{signal('此浏览器未提供全屏，可按 H 隐藏面板','waiting');}};
document.addEventListener('fullscreenchange',()=>$('fullscreen').textContent=document.fullscreenElement?'退出全屏 ↙':'全屏 ↗');
const liftKeys=['Space','ArrowUp','KeyW'];
window.addEventListener('keydown',e=>{if(e.target instanceof HTMLInputElement||e.target instanceof HTMLSelectElement)return;if(liftKeys.includes(e.code)){
  // Keep Space/Enter available for focused buttons; keyboard flight works on the stage.
  if(e.code==='Space'&&e.target instanceof HTMLButtonElement)return;
  e.preventDefault();if(!e.repeat&&!inputHeld){inputHeld=true;lift('keyboard');}
}else if(!e.repeat){if(e.code==='KeyE'&&!paused()&&mode==='keyboard')sim.power();if(e.code==='KeyP')togglePause();if(e.code==='KeyR')reset();if(e.code==='KeyH')document.body.classList.toggle('immersive');}});
window.addEventListener('keyup',e=>{if(liftKeys.includes(e.code))inputHeld=false;});
window.addEventListener('blur',()=>{inputHeld=false;gesture.reset();});
document.addEventListener('visibilitychange',()=>{if(document.hidden){manualPause=true;gesture.reset();$('pause').innerHTML='继续 <kbd>P</kbd>';}});
// Pointer interactions release button focus so the next Space always controls the spirit.
document.querySelectorAll('button').forEach(b=>b.addEventListener('pointerup',()=>setTimeout(()=>b.blur(),0)));
document.querySelectorAll('button').forEach(b=>b.addEventListener('click',e=>{if(e.detail>0)b.blur();}));

async function startCamera(){
  if(loading||cameraActive)return;const generation=++cameraGeneration;loading=true;$('camera-toggle').disabled=true;signal('正在准备本机手部识别模型…','waiting');
  let acquired=null;
  try{
    if(!navigator.mediaDevices?.getUserMedia)throw new Error('浏览器未提供摄像头，请用 localhost 地址打开。');
    // Acquire permission directly from the visitor's click, before loading the model.
    acquired=await navigator.mediaDevices.getUserMedia({video:{width:{ideal:640},height:{ideal:480},facingMode:'user'},audio:false});
    if(generation!==cameraGeneration){acquired.getTracks().forEach(t=>t.stop());return;}
    stream=acquired;video.srcObject=stream;await video.play();
    if(!detector){const {FilesetResolver,HandLandmarker}=await import('./vendor/vision_bundle.mjs');const vision=await FilesetResolver.forVisionTasks(new URL('./vendor/wasm',import.meta.url).href);detector=await HandLandmarker.createFromOptions(vision,{baseOptions:{modelAssetPath:new URL('./vendor/hand_landmarker.task',import.meta.url).href,delegate:'CPU'},runningMode:'VIDEO',numHands:1,minHandDetectionConfidence:.65,minHandPresenceConfidence:.65,minTrackingConfidence:.6});}
    if(generation!==cameraGeneration){acquired.getTracks().forEach(t=>t.stop());return;}
    cameraActive=true;handPresent=false;lastVideoTime=-1;lastHand=0;gesture.reset();$('camera-placeholder').hidden=true;$('camera-toggle').textContent='关闭摄像头';$('calibrate').disabled=false;signal('请把一只手放入画面，先张开手掌','waiting');
    stream.getVideoTracks()[0].addEventListener('ended',()=>{if(cameraActive){stopCamera();signal('摄像头连接中断，请重新开启或切换键盘','error');}});
  }catch(error){if(generation!==cameraGeneration)return;acquired?.getTracks().forEach(t=>t.stop());stopCamera();const errors={NotAllowedError:'未获得摄像头权限。请在浏览器地址栏允许摄像头后重试。',NotFoundError:'没有找到摄像头，请接入摄像头或使用键盘。',NotReadableError:'摄像头暂时无法读取，请关闭其他占用它的应用后重试。'};signal(errors[error.name]||'识别未能启动：'+error.message,'error');console.error(error);
  }finally{loading=false;$('camera-toggle').disabled=false;}
}
function stopCamera(){cameraGeneration++;cameraActive=false;handPresent=false;calibration=null;gesture.reset();stream?.getTracks().forEach(t=>t.stop());stream=null;video.srcObject=null;hc.clearRect(0,0,320,240);$('camera-placeholder').hidden=false;$('camera-toggle').textContent='开启摄像头';$('calibrate').disabled=true;$('calibrate').textContent='校准我的手势';}
$('camera-toggle').onclick=()=>{if(cameraActive){stopCamera();signal('摄像头已关闭，可重新开启或切换键盘','waiting');}else startCamera();};
$('calibrate').onclick=()=>{calibration={phase:'open',samples:[],start:0,open:0};gesture.reset();$('calibrate').textContent='重新开始校准';signal('校准 1/2：张开手掌，保持约 1 秒','waiting');};
function calibrate(score,now){
  if(score===null){calibration.samples=[];calibration.start=0;signal('请把手放回画面，保持'+(calibration.phase==='open'?'张开手掌':'握拳'),'waiting');return;}
  if(!calibration.start)calibration.start=now;
  const plausible=calibration.phase==='open'?score>1.5:score<calibration.open-.35;
  if(!plausible){calibration.samples=[];calibration.start=now;signal(calibration.phase==='open'?'校准 1/2：请完全张开四根手指':'校准 2/2：请握拳并保持','waiting');return;}
  calibration.samples.push(score);
  if(now-calibration.start<1000||calibration.samples.length<10)return;
  const sorted=calibration.samples.slice().sort((a,b)=>a-b),median=sorted[Math.floor(sorted.length/2)];
  if(calibration.phase==='open'){calibration.open=median;calibration.phase='closed';calibration.samples=[];calibration.start=0;signal('校准 2/2：现在握拳，保持约 1 秒','waiting');}
  else{gesture.openThreshold=median+(calibration.open-median)*.72;gesture.closedThreshold=median+(calibration.open-median)*.28;calibration=null;gesture.reset();$('calibrate').textContent='重新校准手势';signal('校准完成。先张开，再握拳。');}
}
function trackHand(now){
  if(!cameraActive||video.readyState<2||now-lastInference<66||video.currentTime===lastVideoTime)return;
  lastInference=now;lastVideoTime=video.currentTime;
  try{
    const result=detector.detectForVideo(video,now),points=result.landmarks[0];
    // Correct the normalized x/y aspect ratio before evaluating finger geometry.
    const score=handOpenness(points?.map(p=>({...p,x:p.x*video.videoWidth/video.videoHeight,z:p.z*video.videoWidth/video.videoHeight})));
    drawHand(points);
    if(points){lastHand=now;handPresent=true;}else if(now-lastHand>450){handPresent=false;}
    if(calibration){calibrate(score,now);return;}
    if(manualPause){gesture.reset();return;}
    const accepted=gesture.update(score,now);
    if(!points){signal('未检测到手 · 将手放回画面，先张开手掌','waiting');return;}
    if(accepted)lift('camera');
    if(gesture.state==='open')signal('已识别：张开手掌 · 现在握拳即可升起');
    else if(gesture.state==='closed')signal('已识别：握拳 · 请张开手掌，准备下一次');
    else signal('手已进入画面 · 请完整张开手掌','waiting');
  }catch(error){stopCamera();signal('手部识别中断，请重新开启或切换键盘','error');console.error(error);}
}
function drawHand(points){hc.clearRect(0,0,320,240);if(!points)return;const chains=[[0,1,2,3,4],[0,5,6,7,8],[5,9,10,11,12],[9,13,14,15,16],[13,17,18,19,20],[0,17]];hc.strokeStyle='#b8d7ff';hc.lineWidth=2;for(const chain of chains){hc.beginPath();chain.forEach((n,i)=>{const p=points[n];i?hc.lineTo(p.x*320,p.y*240):hc.moveTo(p.x*320,p.y*240);});hc.stroke();}hc.fillStyle='#ffdea1';for(const p of points){hc.beginPath();hc.arc(p.x*320,p.y*240,3,0,Math.PI*2);hc.fill();}}
window.addEventListener('pagehide',()=>{stopCamera();detector?.close();});

function ellipse(x,y,rx,ry,color){ctx.fillStyle=color;ctx.beginPath();ctx.ellipse(x,y,rx,ry,0,0,Math.PI*2);ctx.fill();}
function line(points,color,width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(...p):ctx.moveTo(...p));ctx.stroke();}
function glow(x,y,r,color){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,color);g.addColorStop(1,'transparent');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
let environment=atmosphereAt(0);
loadAtmosphere().then(ready=>{if(!ready)$('asset-status').textContent='部分背景未加载，请刷新页面。';else $('asset-status').hidden=true;});
function drawBackground(){
  if(scene==='black'){ctx.fillStyle='#000';ctx.fillRect(0,0,W,H);return;}
  environment=drawAtmosphere(ctx,W,H,scene,sim,$('time-of-day').value);
}
function updateModeHints(){
  const descriptions={gates:'穿过五道光门；碰撞会回到当前门重试。',gather:'追逐 8 束萤火，靠近并聚光，让它们随你前行。',resonance:'在星光靠近时释放波纹，唤醒 6 颗星。'};
  const rules={gates:'穿门 +100 · 连续通过加分 · 门心再 +50',gather:'收集 +80 · 连续收集加分 · 错过 −10，萤火会返回',resonance:'唤醒 +120 · 连续回应加分 · 错过 −10，星光会返回'};
  const activeMode=sim.challenge?sim.playMode:'gates';
  const action=activeMode==='gather'?'聚光':activeMode==='resonance'?'共鸣':'跃起';
  $('scene-subtitle').textContent=scene==='black'?'固定中心 · 纯黑角色展示':!sim.challenge?'自由练习 · 通过背景移动感受升降':descriptions[sim.playMode];$('level-help').textContent=rules[sim.playMode];
  $('power').hidden=activeMode==='gates'||mode!=='keyboard'||!sim.challenge;$('power').textContent=action+' E';
  $('action-hint').innerHTML=mode==='camera'?'张开 → 握拳 · 升起'+(activeMode==='gates'?'':'并'+action):'<kbd>SPACE</kbd> 升起'+(activeMode==='gates'?'':'并'+action+' · <kbd>E</kbd> 只'+action);
  $('keyboard-instruction').innerHTML=activeMode==='gates'?'每按一次，向上跃起一次。<br>松开后可再次触发。':'空格：升起并'+action+'。<br>E：只'+action+'，不改变高度。';
  $('gesture-instruction').textContent='张开手掌 → 握拳：升起一次'+(activeMode==='gates'?'。':'，同时释放'+action+'。')+'再张开后可重复；保持握拳不会连发。';
}
function drawTargets(){
  if(scene==='black'||!sim.challenge||sim.playMode==='gates')return;
  for(const target of sim.targets){if(target.x>1.2)continue;const x=target.x*W,y=worldToScreen(target.x,target.y,sim.y).y*H;
    if(!target.active){if(target.echo>0){const a=target.echo/1.4;glow(x,y,50,'rgba(165,217,255,'+a*.4+')');ellipse(x,y,3,3,'rgba(226,244,255,'+a+')');line([[W*.5,H*.5],[x,y]],'rgba(147,205,255,'+a*.24+')');}continue;}
    const rgb=sim.playMode==='gather'?'255,213,129':'185,214,255',near=Math.hypot((target.x-.5)*sim.aspect,target.y-sim.y)<.35;
    glow(x,y,near?36:22,`rgba(${rgb},${near?.27:.16})`);
    if(sim.playMode==='gather'){
      ellipse(x,y,3.5,3.5,'#ffe6a6');for(const sign of [-1,1])ellipse(x+sign*7,y-3,5,2+Math.sin(sim.time*18+target.index)**2*3,'#eed49e77');
    }else{
      ctx.strokeStyle=near?'#c1e3ff77':'#b7d6ff33';ctx.lineWidth=1;ctx.beginPath();ctx.arc(x,y,H*.075,0,Math.PI*2);ctx.stroke();
      ctx.save();ctx.translate(x,y);ctx.rotate(Math.PI/4);ctx.fillStyle='#d4eaff';ctx.fillRect(-5,-5,10,10);ctx.restore();
      if(near){ctx.textAlign='center';ctx.font='11px sans-serif';ctx.fillStyle='#d0e6f5';ctx.fillText('现在回应',x,y-25);}
    }
  }
}
function drawPowers(){
  ctx.save();ctx.globalCompositeOperation='screen';
  for(const wave of sim.waves){
    const x=W*.5,y=H*.5,r=wave.radius*H,a=Math.max(0,1-wave.age/2.6);
    glow(x,y,Math.min(r+30,H*.35),`rgba(100,174,255,${a*.055})`);
    ctx.shadowColor='#69bfff';ctx.shadowBlur=10;ctx.strokeStyle=`rgba(134,204,255,${a*.7})`;ctx.lineWidth=1.8;
    ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.stroke();
    ctx.shadowBlur=0;ctx.strokeStyle=`rgba(166,220,255,${a*.19})`;ctx.lineWidth=1;
    ctx.beginPath();ctx.arc(x,y,Math.max(0,r-H*.035),0,Math.PI*2);ctx.stroke();
    for(let i=0;i<14;i++){const angle=i*Math.PI*2/14+wave.age*.07;glow(x+Math.cos(angle)*r,y+Math.sin(angle)*r,5,`rgba(174,222,255,${a*.55})`);}
  }ctx.restore();
  if(sim.magnet>0){const a=sim.magnet/1.2;ctx.strokeStyle=`rgba(246,212,137,${a*.22})`;ctx.lineWidth=1;ctx.beginPath();ctx.ellipse(W*.5,H*.5,W*.23,H*.23,0,0,Math.PI*2);ctx.stroke();for(let i=0;i<8;i++){const r=(1-(sim.time*1.1+i*.12)%1)*.2,angle=i*2.4;ellipse(W*(.5+Math.cos(angle)*r),H*(.5+Math.sin(angle)*r),1.4,1.4,`rgba(255,218,153,${a*.7})`);}}
}
function drawAwakened(){
  if(scene==='black')return;
  if(sim.playMode!=='gates'){for(let i=0;i<sim.passed;i++){const x=W*(.45-i*.025),y=H*(.5+Math.sin(sim.time*1.2+i)*.04);glow(x,y,14,scene==='forest'?'#f9d89835':'#bad9ff35');ellipse(x,y,2,2,scene==='forest'?'#ffe4b1':'#d7e9ff');if(i>0)line([[x,y],[x+W*.025,H*(.5+Math.sin(sim.time*1.2+i-1)*.04)]],'#b9d9ed44');}return;}
  for(let i=0;i<sim.passed;i++){
    const x=W*(.12+i*.055),y=H*.82+Math.sin(i*1.7)*8+(.5-sim.y)*H*.3;
    glow(x,y,28,scene==='forest'?'#ebc98726':'#b9d1ff26');
    if(scene==='forest'){line([[x,y+22],[x,y]],'#8ea18b88');for(let p=0;p<5;p++)ellipse(x+Math.cos(p*Math.PI*.4)*5,y+Math.sin(p*Math.PI*.4)*5,3,3,'#e8cf91aa');}
    else{ellipse(x,y,3,3,'#e3edff');if(i>0)line([[x-W*.055,y-8],[x,y]],'#bacfff77');}
  }
}
function drawTrail(){const rgb=scene==='stars'?'219,231,255':scene==='geometry'?'150,181,255':'235,200,134';
  ctx.lineCap='round';for(let i=1;i<sim.trail.length;i++){const a=sim.trail[i-1],b=sim.trail[i];if(scene==='geometry'&&i%5!==0)continue;line([[a.x*W,worldToScreen(a.x,a.y,sim.y).y*H],[b.x*W,worldToScreen(b.x,b.y,sim.y).y*H]],`rgba(${rgb},${b.life*.32})`,scene==='geometry'?2:1.4);}
  for(const p of sim.particles){ellipse(p.x*W,worldToScreen(p.x,p.y,sim.y).y*H,p.size,p.size,`rgba(${rgb},${p.life*.75})`);}
}
function drawObstacles(){if(scene==='black')return;
  for(const gate of sim.gates){if(gate.x>1.1)continue;const x=gate.x*W,cy=worldToScreen(gate.x,gate.y,sim.y).y*H,top=cy-gate.gap*H/2,bottom=cy+gate.gap*H/2;
    ctx.save();ctx.strokeStyle=gate.passed?'#cbb77955':scene==='forest'?'#a7bcae88':'#a5bfffaa';ctx.fillStyle=scene==='forest'?'#17312ce6':'#283951aa';ctx.lineWidth=2;
    for(const [a,b]of [[-H,top],[bottom,H*2]]){ctx.fillRect(x-14,a,28,b-a);ctx.strokeRect(x-14,a,28,b-a);line([[x-24,a],[x+24,a]],'#bfd1b899',2);line([[x-24,b],[x+24,b]],'#bfd1b899',2);}
    if(scene==='stars'){ctx.strokeStyle='#c6d6ff66';ctx.beginPath();ctx.ellipse(x,cy,gate.gap*H*.17,gate.gap*H/2,0,0,Math.PI*2);ctx.stroke();}
    if(!gate.passed){glow(x,cy,40,'#d5d9cb12');ctx.save();ctx.translate(x,cy);ctx.rotate(Math.PI/4);ctx.fillStyle='#e9d5aa';ctx.fillRect(-4,-4,8,8);ctx.restore();ctx.font='10px sans-serif';ctx.textAlign='center';ctx.fillStyle='#a9b8c9';ctx.fillText(String(gate.index+1).padStart(2,'0'),x,top-13);}
    glow(x,top,25,'#bcd7da22');glow(x,bottom,25,'#bcd7da22');ctx.restore();
  }
}
function drawSpirit(t){
  const x=W*.5,y=H*.5,scale=Math.min(H/720,1.3);
  const color=sim.hit>0?'#ffd1b1':scene==='geometry'?'#d7e7ff':'#fff0cf';
  glow(x,y,100*scale,scene==='geometry'?`rgba(119,164,255,${.10+sim.glow*.12})`:`rgba(237,200,130,${.10+sim.glow*.13})`);
  
  ctx.save();ctx.translate(x,y);ctx.scale(scale,scale);
  if(sim.protection>0)ctx.globalAlpha=.65+Math.sin(t*20)*.25;
  ctx.shadowColor=scene==='geometry'?'#9abcff':'#f3d7a1';ctx.shadowBlur=18+sim.glow*22;ctx.fillStyle=color;ctx.strokeStyle=color;ctx.lineCap='round';ctx.lineJoin='round';
  const rising=sim.vy<-.04,falling=sim.vy>.04,breath=Math.sin(t*2)*1.3;
  // Original humanoid silhouette: seed-shaped head, tapered torso and four limbs.
  ctx.beginPath();ctx.moveTo(-9,-25);ctx.bezierCurveTo(-16,-45,11,-49,13,-31);ctx.bezierCurveTo(17,-18,1,-13,-9,-25);ctx.fill();
  ctx.beginPath();ctx.moveTo(-6,-16);ctx.bezierCurveTo(-8,-7,-9,4,-6,11);ctx.quadraticCurveTo(0,17,7,9);ctx.bezierCurveTo(8,1,7,-10,5,-16);ctx.closePath();ctx.fill();
  ctx.lineWidth=5;
  ctx.beginPath();ctx.moveTo(-5,-11);ctx.quadraticCurveTo(-17,-4,-23,rising?-23:falling?-13:6+breath);ctx.moveTo(5,-11);ctx.quadraticCurveTo(17,-8,23,rising?-27:falling?-17:3-breath);ctx.stroke();
  ctx.lineWidth=5.5;ctx.beginPath();ctx.moveTo(-4,9);ctx.quadraticCurveTo(-7,23,-12+(rising?4:0),32+(rising?4:0));ctx.moveTo(4,9);ctx.quadraticCurveTo(7,22,12,29+(rising?9:0));ctx.stroke();
  // Two small, cool eyes provide a readable face at prototype scale.
  ctx.shadowBlur=0;ellipse(-3,-32,1.3,2.1,'#394b58');ellipse(5,-33,1.3,2.1,'#394b58');
  glow(0,-3,15,'#ffffff55');ctx.restore();
  if(sim.glow>0){ctx.strokeStyle=`rgba(225,222,194,${sim.glow*.35})`;ctx.lineWidth=1;ctx.beginPath();ctx.arc(x,y,(1-sim.glow)*70+22,0,Math.PI*2);ctx.stroke();}
}
function updateUI(){
  const waiting=isWaiting();
  $('count').textContent=String(sim.count).padStart(2,'0');$('phase-label').textContent=environment.name;$('journey-clock').textContent=String(Math.floor(sim.elapsed/60)).padStart(2,'0')+':'+String(Math.floor(sim.elapsed%60)).padStart(2,'0');$('period-line').hidden=scene==='black';
  $('level-progress').hidden=scene==='black';$('level-progress').textContent=sim.challenge?('第 '+(sim.levelIndex+1)+' / 3 关 · 已点亮 '+sim.passed+' / '+sim.goal+' · '+sim.score+' 分'):'自由练习 · 角色固定在画面中央';
  $('award').textContent=sim.time-sim.awardTime<2.5?sim.lastAward:'';
  $('chapter-complete').hidden=!sim.complete;
  if(sim.complete){archiveRun();$('completion-title').textContent=sim.level.name+' · 已点亮';$('completion-score').textContent=sim.score+' 分 · '+(sim.playMode==='gates'?sim.collisions+' 次轻碰':sim.missed+' 次错过')+' · '+sim.elapsed.toFixed(1)+' 秒';$('next-level').textContent=sim.levelIndex===2?'重新踏上旅程 ↻':'前往 '+LEVELS[Math.min(2,sim.levelIndex+1)].name+' →';}
  updateRanking();
  $('paused-overlay').hidden=!manualPause&&!waiting;
  if(manualPause){$('pause-title').textContent='片刻停留';$('pause-description').textContent='按 P 或点击继续';}
  else if(waiting){$('pause-title').textContent=calibration?'认识你的手势':cameraActive?'等待你的手':'等待开启摄像头';$('pause-description').textContent=calibration?'跟随右侧提示，先张开手掌，再握拳':cameraActive?'把一只手放入画面，先张开，再握拳':'点击右侧开启摄像头，或切换键盘继续';}
  if(sim.started&&sim.glow<.2)$('activity').textContent=sim.complete?'本关完成 · 世界回应了你':sim.hit>0?'从这道门再试一次':sim.y>=.82?'轻按空格 · 继续向上':sim.vy<0?'正在上升 · 世界向下移动':'正在下落 · 世界向上移动';
}
function frame(now){const dt=Math.min((now-frameTime)/1000,.04);frameTime=now;trackHand(now);
  if(cameraActive && now-lastInference>900){handPresent=false;gesture.reset();signal('摄像头暂无新画面，请重新开启或切换键盘','waiting');}
  if(!paused())sim.update(dt,$('obstacles').checked);
  if(projectionWindow?.closed){projectionWindow=null;splitProjection=false;}
  ctx=mainContext;
  drawBackground();drawAwakened();drawObstacles();drawTargets();
  if(splitProjection){
    const g=ctx.createRadialGradient(W*.5,H*.5,0,W*.5,H*.5,Math.min(W,H)*.48);
    g.addColorStop(0,'rgba(0,0,0,.28)');g.addColorStop(.35,'rgba(0,0,0,.20)');g.addColorStop(.72,'rgba(0,0,0,.065)');g.addColorStop(1,'rgba(0,0,0,0)');ctx.fillStyle=g;ctx.fillRect(0,0,W,H);
    renderProjection();
  }else{drawTrail();drawPowers();drawSpirit(sim.time);}
  if(now-lastUI>100){updateUI();lastUI=now;}requestAnimationFrame(frame);
}
setScene('forest');
requestAnimationFrame(frame);

// Both outputs render the same simulation in the same frame; no second physics clock.
const projectionButton=document.createElement('button');
projectionButton.className='quiet full';projectionButton.textContent='开启双屏投影 · 人物独立窗口';
$('immersive').before(projectionButton);
projectionButton.onclick=()=>{
  if(projectionWindow&&!projectionWindow.closed){projectionWindow.focus();return;}
  projectionWindow=window.open('','echoes-character','popup,width=1000,height=720');
  if(!projectionWindow){signal('请允许弹出窗口，再开启双屏投影','waiting');return;}
  const d=projectionWindow.document;
  d.open();d.write(`<!doctype html><html lang="zh"><meta charset="utf-8"><title>光的回响 · 人物投影屏</title><style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}canvas{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%)}nav{position:fixed;top:12px;left:12px;color:#ddd;font:14px sans-serif;background:#151515;padding:10px;border-radius:8px}button{margin:5px}body.hide nav{display:none}</style><canvas></canvas><nav>拖到第二块屏幕；双击画面全屏。H 隐藏提示，M 镜像。<br><button id="full">全屏并隐藏提示</button><button id="mirror">左右镜像</button><button id="hide">隐藏提示</button></nav></html>`);d.close();
  projectionCanvas=d.querySelector('canvas');projectionCanvas.mirrored=false;
  const full=()=>{d.body.classList.add('hide');d.documentElement.requestFullscreen?.().catch(()=>{});};
  d.querySelector('#full').onclick=full;projectionCanvas.ondblclick=full;
  d.querySelector('#mirror').onclick=()=>projectionCanvas.mirrored=!projectionCanvas.mirrored;
  d.querySelector('#hide').onclick=()=>d.body.classList.add('hide');
  projectionWindow.onkeydown=e=>{if(e.code==='KeyH')d.body.classList.toggle('hide');else if(e.code==='KeyM')projectionCanvas.mirrored=!projectionCanvas.mirrored;else if(liftKeys.includes(e.code)&&!e.repeat){e.preventDefault();lift('keyboard');}else if(e.code==='KeyE'&&!paused())sim.power();else if(e.code==='KeyP')togglePause();};
  splitProjection=true;projectionButton.textContent='人物窗口已开启 · 点击定位窗口';
};
function renderProjection(){
  if(!projectionWindow||projectionWindow.closed)return;
  const pc=projectionCanvas.getContext('2d'),ratio=Math.min(projectionWindow.innerWidth/W,projectionWindow.innerHeight/H),pd=Math.min(projectionWindow.devicePixelRatio||1,2);
  const width=Math.round(W*ratio*pd),height=Math.round(H*ratio*pd);
  if(projectionCanvas.width!==width||projectionCanvas.height!==height){projectionCanvas.width=width;projectionCanvas.height=height;}
  projectionCanvas.style.width=(W*ratio)+'px';projectionCanvas.style.height=(H*ratio)+'px';
  pc.setTransform(width/W,0,0,height/H,0,0);pc.fillStyle='#000';pc.fillRect(0,0,W,H);
  pc.save();if(projectionCanvas.mirrored){pc.translate(W,0);pc.scale(-1,1);}
  ctx=pc;drawTrail();drawPowers();drawSpirit(sim.time);pc.restore();ctx=mainContext;
}
window.addEventListener('pagehide',()=>projectionWindow?.close());
