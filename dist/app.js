import {ContinuitySimulation,CHAPTERS,band} from './continuity.js';
import {GestureGate,handOpenness,clamp,LEVELS,worldToScreen} from './core.js';
import {projectionSpiritScale,renderSpirit} from './spirit-renderer.js';
import {drawContinuity} from './continuity-render.js';
import {loadAtmosphere,drawAtmosphere,atmosphereAt} from './atmosphere.js';
import {installInteractionControls} from './interaction-controls.js';
import {installExhibitGame} from './exhibit-game.js';
const $=id=>document.getElementById(id),canvas=$('world'),video=$('webcam'),handCanvas=$('hand-overlay'),hc=handCanvas.getContext('2d');
let ctx=canvas.getContext('2d');const mainContext=ctx;
const sim=new ContinuitySimulation(),gesture=new GestureGate();
let scene='forest',blackDisplay=false,manualPause=false,cameraActive=false,loading=false,stream=null,detector=null;
let handPresent=false,lastInference=0,lastVideoTime=-1,frameTime=performance.now(),lastUI=0,calibration=null,cameraGeneration=0;
let W=1000,H=800,dpr=1,projectionWindow=null,projectionCanvas=null,splitProjection=false;
let savedRevision=0;
const CHECKPOINT_KEY='echoes-body-v2';
const PROJECTION_LABEL='开启双屏投影 · 人物独立窗口';
function resize(){const r=canvas.getBoundingClientRect();W=r.width;H=r.height;sim.aspect=W/Math.max(1,H);dpr=Math.min(devicePixelRatio||1,2);canvas.width=W*dpr;canvas.height=H*dpr;ctx.setTransform(dpr,0,0,dpr,0,0);}
new ResizeObserver(resize).observe(canvas);
function showText(id,value){
 const element=$(id),text=String(value);
 if(element.textContent!==text)element.textContent=text;
}
function signal(text,type='ready'){showText('signal-text',text);$('signal-dot').style.background=type==='error'?'#d69583':type==='waiting'?'#c3ae7e':'#92bba6';}
function isWaiting(){return !cameraActive||!handPresent||!!calibration||!sim.audioReady||sim.hand==='unknown';}
function paused(){return manualPause||isWaiting();}
function saveCheckpoint(){
 try{localStorage.setItem(CHECKPOINT_KEY,JSON.stringify(sim.save()));}catch{}
 savedRevision=sim.checkpointRevision;
}
function saveIfChanged(){if(sim.checkpointRevision!==savedRevision)saveCheckpoint();}
function syncSceneControls(){
 document.body.classList.toggle('black-scene',blackDisplay);
 showText('black-display',blackDisplay?'返回场景画面':'纯黑人物展示');
 for(const button of document.querySelectorAll('[data-scene]')){
  const selected=button.dataset.scene===(blackDisplay?'black':scene);
  button.classList.toggle('selected',selected);
  button.setAttribute('aria-pressed',String(selected));
 }
}
function setScene(next,checkpoint=false){
 if(next==='black'){
  blackDisplay=!blackDisplay;
 }else{
  saveIfChanged();
  scene=next;blackDisplay=false;
  sim.reset(LEVELS.findIndex(level=>level.id===next));
  sim.configureChallenge(true);
  gesture.reset();manualPause=false;
  savedRevision=sim.checkpointRevision;
  if(checkpoint)saveCheckpoint();
 }
 syncSceneControls();updateUI();
}
function clickInput(choice){
 if(paused())return false;
 const action=sim.click(choice);
 if(action==='next')setScene(LEVELS[sim.levelIndex+1].id,true);
 return Boolean(action);
}
function togglePause(){manualPause=!manualPause;sim.cancelInput();gesture.reset();updateUI();}
function clickKey(event){
 if(event.code!=='Space'||event.altKey||event.ctrlKey||event.metaKey)return false;
 if(event.target?.closest?.('[data-touch-option]'))return false;
 event.preventDefault();
 if(!event.repeat)clickInput();
 return true;
}
window.addEventListener('keydown',clickKey);
document.addEventListener('visibilitychange',()=>{if(document.hidden){manualPause=true;sim.cancelInput();gesture.reset();}});
$('pause').onclick=togglePause;$('reset').onclick=()=>{sim.retry();gesture.reset();updateUI();};
$('immersive').onclick=()=>document.body.classList.toggle('immersive');
$('fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen();}catch{signal('浏览器暂不支持全屏。','waiting');}};
$('time-of-day').onchange=()=>$('time-of-day').blur();
document.querySelectorAll('[data-scene]').forEach(b=>b.onclick=()=>setScene(b.dataset.scene));
$('resume-journey').onclick=()=>{
 let checkpoint;
 try{checkpoint=JSON.parse(localStorage.getItem(CHECKPOINT_KEY));}catch{}
 if(!sim.restore(checkpoint)){signal('没有可恢复的当前版本检查点。','waiting');return;}
 scene=LEVELS[sim.levelIndex].id;blackDisplay=false;
 gesture.reset();manualPause=false;savedRevision=sim.checkpointRevision;
 syncSceneControls();updateUI();
};
window.addEventListener('pagehide',saveIfChanged);
async function startCamera(){
  if(loading||cameraActive)return;const generation=++cameraGeneration;loading=true;$('camera-toggle').disabled=true;signal('正在准备本机手部识别模型…','waiting');
  let acquired=null;
  try{
    if(!navigator.mediaDevices?.getUserMedia)throw new Error('浏览器未提供摄像头，请用 localhost 地址打开。');
    // Acquire permission directly from the visitor's click, before loading the model.
    acquired=await navigator.mediaDevices.getUserMedia({video:{width:{ideal:640},height:{ideal:480},facingMode:'user'},audio:false});
    if(generation!==cameraGeneration){acquired.getTracks().forEach(t=>t.stop());return;}
    stream=acquired;
    const track=stream.getVideoTracks()[0];
    track.addEventListener('ended',()=>{
      if(generation!==cameraGeneration)return;
      stopCamera();signal('摄像头连接中断，请重新开启','error');
    });
    video.srcObject=stream;await video.play();
    if(!detector){const {FilesetResolver,HandLandmarker}=await import('./vendor/vision_bundle.mjs');const vision=await FilesetResolver.forVisionTasks(new URL('./vendor/wasm',import.meta.url).href);detector=await HandLandmarker.createFromOptions(vision,{baseOptions:{modelAssetPath:new URL('./vendor/hand_landmarker.task',import.meta.url).href,delegate:'CPU'},runningMode:'VIDEO',numHands:1,minHandDetectionConfidence:.65,minHandPresenceConfidence:.65,minTrackingConfidence:.6});}
    if(generation!==cameraGeneration){acquired.getTracks().forEach(t=>t.stop());return;}
    cameraActive=true;handPresent=false;lastVideoTime=-1;lastInference=performance.now();gesture.reset();$('camera-placeholder').hidden=true;$('camera-toggle').textContent='关闭摄像头';$('calibrate').disabled=false;signal('请把一只手放入画面，先张开手掌','waiting');
  }catch(error){if(generation!==cameraGeneration)return;acquired?.getTracks().forEach(t=>t.stop());stopCamera();const errors={NotAllowedError:'未获得摄像头权限。请在浏览器地址栏允许摄像头后重试。',NotFoundError:'没有找到摄像头，请接入摄像头后重试。',NotReadableError:'摄像头暂时无法读取，请关闭其他占用它的应用后重试。'};signal(errors[error.name]||'识别未能启动：'+error.message,'error');console.error(error);
  }finally{loading=false;$('camera-toggle').disabled=false;}
}
function stopCamera(){sim.cancelHold();cameraGeneration++;cameraActive=false;handPresent=false;calibration=null;gesture.reset();stream?.getTracks().forEach(t=>t.stop());stream=null;video.srcObject=null;hc.clearRect(0,0,320,240);$('camera-placeholder').hidden=false;$('camera-toggle').textContent='开启摄像头';$('calibrate').disabled=true;$('calibrate').textContent='校准我的手势';}
$('camera-toggle').onclick=()=>{if(cameraActive){stopCamera();signal('摄像头已关闭，可重新开启','waiting');}else startCamera();};
$('calibrate').onclick=()=>{sim.cancelHold();calibration={phase:'open',samples:[],start:0,open:0};gesture.reset();$('calibrate').textContent='重新开始校准';signal('校准 1/2：张开手掌，保持约 1 秒','waiting');};
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
    handPresent=Number.isFinite(score);
    if(!handPresent){gesture.reset();sim.cancelInput();}
    if(calibration){calibrate(score,now);return;}
    if(!handPresent){signal('未检测到手 · 将手放回画面，先张开手掌','waiting');return;}
    if(manualPause||!sim.audioReady){gesture.reset();sim.cancelInput();return;}
    gesture.update(score,now);
    if(['open','closed'].includes(gesture.state))sim.setHand(gesture.state);
    if(sim.hand==='open')signal('已识别：张手 · 世界正在接收你的动作');
    else if(sim.hand==='closed')signal('已识别：握拳 · 光收在掌心');
    else signal('手已进入画面 · 请完整张开手掌','waiting');
  }catch(error){stopCamera();signal('手部识别中断，请重新开启','error');console.error(error);}
}
function drawHand(points){hc.clearRect(0,0,320,240);if(!points)return;const chains=[[0,1,2,3,4],[0,5,6,7,8],[5,9,10,11,12],[9,13,14,15,16],[13,17,18,19,20],[0,17]];hc.strokeStyle='#b8d7ff';hc.lineWidth=2;for(const chain of chains){hc.beginPath();chain.forEach((n,i)=>{const p=points[n];i?hc.lineTo(p.x*320,p.y*240):hc.moveTo(p.x*320,p.y*240);});hc.stroke();}hc.fillStyle='#ffdea1';for(const p of points){hc.beginPath();hc.arc(p.x*320,p.y*240,3,0,Math.PI*2);hc.fill();}}
window.addEventListener('pagehide',()=>{stopCamera();detector?.close();});

function ellipse(x,y,rx,ry,color){ctx.fillStyle=color;ctx.beginPath();ctx.ellipse(x,y,rx,ry,0,0,Math.PI*2);ctx.fill();}
function line(points,color,width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(...p):ctx.moveTo(...p));ctx.stroke();}
function glow(x,y,r,color){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,color);g.addColorStop(1,'transparent');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
let environment=atmosphereAt(0);
loadAtmosphere().then(ready=>{if(!ready)$('asset-status').textContent='部分背景未加载，请刷新页面。';else $('asset-status').hidden=true;});
function drawBackground(){
  if(blackDisplay){ctx.fillStyle='#000';ctx.fillRect(0,0,W,H);return;}
  environment=drawAtmosphere(ctx,W,H,scene,sim,$('time-of-day').value);
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
function drawTrail(){const rgb=scene==='stars'?'219,231,255':scene==='geometry'?'150,181,255':'235,200,134';
  const strength=sim.trailStrength,scale=Math.max(.7,Math.min(H/720,1.5));
  ctx.save();ctx.globalCompositeOperation='screen';ctx.lineCap='round';
  // Broad coloured bloom, luminous ribbon, then a fine pale core.
  for(const [width,opacity] of [[13,.055],[4.5,.25],[1.25,.72]]){
    for(let i=1;i<sim.trail.length;i++){
      const a=sim.trail[i-1],b=sim.trail[i],energy=.65+(b.energy||0)*.7;
      line([[a.x*W,worldToScreen(a.x,a.y,sim.y).y*H],[b.x*W,worldToScreen(b.x,b.y,sim.y).y*H]],`rgba(${width<2?'255,246,225':rgb},${Math.min(.95,b.life*b.life*opacity*strength*energy)})`,width*scale*(.5+b.life*.5));
    }
  }
  for(const p of sim.particles){const x=p.x*W,y=worldToScreen(p.x,p.y,sim.y).y*H;
    glow(x,y,p.size*4*scale,`rgba(${rgb},${Math.min(.4,p.life*.12*strength)})`);
    ellipse(x,y,p.size*scale,p.size*scale,`rgba(${rgb},${Math.min(1,p.life*.8*strength)})`);
  }ctx.restore();
}
let spiritTime=0;
function drawSpirit({x=W*.5,y=H*.5,scale=Math.min(H/720,1.3),rotation=0,pixelRatio=1,effectScale=1,projectionEffects=false,echoHand=null}={}){
  const castAge=sim.time-sim.lastPower;
  const moving=!paused()&&!sim.complete;
  const cast=moving&&castAge>=0&&castAge<.85?Math.sin(Math.PI*castAge/.85):0;
  const travel=moving&&sim.started?clamp(sim.speedMultiplier*.46+Math.abs(sim.vy)*1.5,0,1):0;
  renderSpirit(ctx,{x,y,scale,rotation,pixelRatio,projectionEffects,time:spiritTime,
    vy:moving&&!echoHand?sim.vy:0,travel:echoHand?0:travel,cast:echoHand?0:cast,
    energy:echoHand?0:sim.glow,hit:echoHand?0:sim.hit,
    protection:echoHand?Number(echoHand==='closed'):sim.protection,
    cool:scene==='geometry'||scene==='stars'});
  if(!echoHand&&sim.glow>0){ctx.strokeStyle=`rgba(225,242,228,${sim.glow*.35})`;ctx.lineWidth=effectScale;ctx.beginPath();ctx.arc(x,y,((1-sim.glow)*70+22)*effectScale,0,Math.PI*2);ctx.stroke();}
}

function shadeSceneCenter(){
  if(blackDisplay)return;
  const centerShade=splitProjection?.56:.40;
  ctx.save();
  ctx.translate(W/2,H/2);
  ctx.scale(W*.48,H*.57);
  const falloff=ctx.createRadialGradient(0,0,0,0,0,1);
  // Zero slope at the center and rim keeps the darker area from reading as a disk.
  for(let step=0;step<=12;step++){
    const radius=step/12;
    const opacity=centerShade*(1-radius*radius*(3-2*radius));
    falloff.addColorStop(radius,`rgba(0,0,0,${opacity})`);
  }
  ctx.fillStyle=falloff;
  ctx.fillRect(-1,-1,2,2);
  ctx.restore();
}


function updateUI(){
 $('scene-index').textContent=sim.tutorial!==null?'00 / LEARNING':String(sim.levelIndex+1).padStart(2,'0')+' / '+['CARE','COOPERATION','DIALOGUE'][sim.levelIndex];
 $('scene-title').textContent=sim.tutorial!==null?'回声庭院':CHAPTERS[sim.levelIndex].title;
 $('scene-subtitle').textContent=sim.tutorial!==null?'你认识世界，世界也在认识你的手与声音。':CHAPTERS[sim.levelIndex].subtitle;
 const ending=sim.complete&&sim.levelIndex===LEVELS.length-1;
 showText('level-progress',sim.complete?(ending?'旅程完成':'本章完成 · 点击继续旅程'):sim.objective);
 showText('journey-feedback',sim.feedback);
 showText('encounter-status',sim.complete?'':sim.cue);
 $('hand-state').textContent=!handPresent?'手未进入画面':sim.hand==='unknown'?'先张开手掌':sim.holding?'握拳':'张手';
 $('voice-state').textContent=sim.audioReady?band(sim.controlVoice)+' '+Math.round(sim.controlVoice*100)+'%':'等待校准';
 $('activity').textContent=sim.complete?'世界记住了你':paused()?'等待真实输入':'真实手势与声音已连接';
 $('phase-label').textContent=environment.name;$('journey-clock').textContent=Math.floor(sim.elapsed/60).toString().padStart(2,'0')+':'+Math.floor(sim.elapsed%60).toString().padStart(2,'0');
 $('count').textContent=sim.count;$('pause').textContent=manualPause?'继续旅程':'暂停旅程';
 $('chapter-complete').hidden=!sim.complete;
 showText('completion-title',ending?'世界学会了回应你':CHAPTERS[sim.levelIndex].title+' · 已唤醒');
 showText('completion-score',sim.finishText);
 showText('completion-next',ending?'可在展台设置里重访任一章节。':'触摸 A 或 B 进入下一章 · 空格也可以');
 $('paused-overlay').hidden=!paused()||ending;
 showText('pause-title',manualPause?'旅程已暂停':!cameraActive?'先开启摄像头':!sim.audioReady?'先开启并校准麦克风':calibration?'正在校准手势':!handPresent?'请把手放回画面':'请先张开手掌');
 $('pause-description').textContent=manualPause?'点击右侧「继续旅程」，再张开手掌。':'开启摄像头并校准麦克风后，触摸 A 或 B 作出选择。等待设备时会保留进度。';
 exhibitGame.updateControls(sim.touchControls,ending);
 saveIfChanged();
}

const interactions=installInteractionControls(sim,paused);
const exhibitGame=installExhibitGame($('touch-game'),$('touch-choices'),{isBlocked:paused,respondToTouch:clickInput});
setScene('forest');
function frame(now){const dt=Math.min((now-frameTime)/1000,.04);frameTime=now;spiritTime+=dt;
 interactions.beforeUpdate(dt,now);trackHand(now);
 if(cameraActive&&now-lastInference>300){
   handPresent=false;sim.cancelInput();gesture.reset();
   signal('未收到新摄像头画面 · 旅程已暂停','waiting');
 }
 if(!paused())sim.update(dt,true);
 interactions.afterUpdate(dt);exhibitGame.update(now);
 if(projectionWindow?.closed){
  projectionWindow=null;projectionCanvas=null;splitProjection=false;
  projectionButton.textContent=PROJECTION_LABEL;
 }
 ctx=mainContext;drawBackground();shadeSceneCenter();
 if(!blackDisplay)drawContinuity(ctx,W,H,sim,(x,y,echoHand)=>drawSpirit({x,y,echoHand,scale:Math.min(H/720,1.3)*.8}));
 if(splitProjection)renderProjection();
 if(!splitProjection||blackDisplay){drawTrail();drawPowers();drawSpirit();}
 if(now-lastUI>100){updateUI();lastUI=now;}requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
// Both outputs render the same simulation in the same frame; no second physics clock.
const projectionButton=document.createElement('button');
projectionButton.className='quiet full';projectionButton.textContent=PROJECTION_LABEL;
$('immersive').before(projectionButton);
projectionButton.onclick=()=>{
  if(projectionWindow&&!projectionWindow.closed){projectionWindow.focus();return;}
  projectionWindow=window.open('','echoes-character','popup,width=1000,height=720');
  if(!projectionWindow){signal('请允许弹出窗口，再开启双屏投影','waiting');return;}
  const d=projectionWindow.document;
  d.open();d.write(`<!doctype html><html lang="zh"><meta charset="utf-8"><title>光的回响 · 人物投影屏</title><style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}canvas{display:block;width:100%;height:100%}nav{position:fixed;top:12px;left:12px;color:#ddd;font:14px sans-serif;background:#151515;padding:10px;border-radius:8px}button{margin:5px}body.hide nav{display:none}</style><canvas></canvas><nav>拖到第二块屏幕；双击画面全屏。H 隐藏提示，M 镜像。<br><button id="full">全屏并隐藏提示</button><button id="mirror">左右镜像</button><button id="hide">隐藏提示</button></nav></html>`);d.close();
  projectionCanvas=d.querySelector('canvas');projectionCanvas.mirrored=false;
  const full=()=>{d.body.classList.add('hide');d.documentElement.requestFullscreen?.().catch(()=>{});};
  d.querySelector('#full').onclick=full;projectionCanvas.ondblclick=full;
  d.querySelector('#mirror').onclick=()=>projectionCanvas.mirrored=!projectionCanvas.mirrored;
  d.querySelector('#hide').onclick=()=>d.body.classList.add('hide');
  projectionWindow.onkeydown=event=>{
   if(clickKey(event)||event.repeat)return;
   if(event.code==='KeyH')d.body.classList.toggle('hide');
   if(event.code==='KeyM')projectionCanvas.mirrored=!projectionCanvas.mirrored;
  };
  splitProjection=true;projectionButton.textContent='人物窗口已开启 · 点击定位窗口';
};
function renderProjection(){
  if(!projectionWindow||projectionWindow.closed)return;
  const width=projectionWindow.innerWidth, height=projectionWindow.innerHeight;
  if(width<=4||height<=4)return;
  const pixelRatio=Math.min(projectionWindow.devicePixelRatio||1,2);
  const pixelsWide=Math.round(width*pixelRatio), pixelsHigh=Math.round(height*pixelRatio);
  if(projectionCanvas.width!==pixelsWide)projectionCanvas.width=pixelsWide;
  if(projectionCanvas.height!==pixelsHigh)projectionCanvas.height=pixelsHigh;

  const pc=projectionCanvas.getContext('2d');
  pc.setTransform(pixelsWide/width,0,0,pixelsHigh/height,0,0);
  pc.fillStyle='#000';
  pc.fillRect(0,0,width,height);
  pc.save();
  if(projectionCanvas.mirrored){pc.translate(width,0);pc.scale(-1,1);}
  ctx=pc;

  const sceneScale=Math.min(width/W,height/H);
  pc.save();
  pc.translate((width-W*sceneScale)/2,(height-H*sceneScale)/2);
  pc.scale(sceneScale,sceneScale);
  drawTrail();
  drawPowers();
  pc.restore();

  drawSpirit({x:width/2,y:height/2,scale:projectionSpiritScale(width,height),
    rotation:-Math.PI/2,pixelRatio,effectScale:sceneScale,projectionEffects:true});
  pc.restore();
  ctx=mainContext;
}
window.addEventListener('pagehide',()=>projectionWindow?.close());
