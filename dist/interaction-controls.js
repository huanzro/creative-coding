import {VolumeEnvelope,volumeSettings,ambientCalibration} from './volume-core.js';
import {MicrophoneInput} from './audio-input.js';
import {SerialOutput,outputLevels,OUTPUT_ZERO} from './physical-output.js';

export function comfortCalibration(noise,voice){
 const quietSamples=noise.filter(Number.isFinite);
 const voicedSamples=voice.filter(Number.isFinite).sort((a,b)=>a-b);
 if(!quietSamples.length||!voicedSamples.length)return null;
 const settings=ambientCalibration(quietSamples);
 const comfortable=voicedSamples[Math.floor((voicedSamples.length-1)*.7)];
 const range=comfortable-settings.quietDb;
 if(range<8)return null;
 // Smoothstep maps 72% of the calibrated dB range to about 81% energy.
 // Leave room above the player's normal hum so the tutorial can accept it.
 const calibrated=volumeSettings({...settings,loudDb:settings.quietDb+range/.72,attack:.14,release:.28});
 const position=range/(calibrated.loudDb-calibrated.quietDb);
 const energy=position*position*(3-2*position);
 return position<=1&&energy>=.55&&energy<=.9?calibrated:null;
}
export function installInteractionControls(sim,isPaused){
 const panel=document.createElement('section');panel.className='control-section interaction-controls';
 panel.innerHTML=`<h3>02 <span>真实声音</span></h3><p class="level-help">先安静 2 秒，再舒适地哼声 3 秒。游戏以你的舒适音量为上限，不需要喊叫。</p><button id="mic-toggle" class="primary full">开启麦克风并校准</button><p id="audio-status" class="level-help" role="status">麦克风未开启，旅程等待声音。</p><meter id="volume-meter" min="0" max="1" value="0" aria-label="真实麦克风声音能量"></meter><div class="sensor-readout"><span id="volume-db">— dBFS</span><strong id="volume-speed">0%</strong></div><button id="audio-calibrate" class="quiet full" disabled>重新校准我的声音</button><h3 class="output-heading">展台硬件</h3><p class="level-help"><a href="exhibit-hardware.html">双灯、舵机与触摸调试</a></p><div class="sensor-readout"><span id="fan-preview">风力 0%</span><span id="light-preview">灯光 0%</span></div><details><summary>原有风力与灯光接口</summary><button id="serial-toggle" class="quiet full">连接 Arduino</button><label class="toggle-line"><span>启用实际输出</span><input id="hardware-arm" type="checkbox" disabled></label><button id="hardware-stop" class="quiet full">停止硬件输出</button><p id="physical-status" class="level-help">尚未连接</p></details>`;
 document.querySelector('aside .control-section').after(panel);
 const $=id=>panel.querySelector('#'+id),envelope=new VolumeEnvelope();
 let calibration=null,ready=false,pending=false,snapshot={db:-100,level:0},lastFrame=-Infinity,levels=OUTPUT_ZERO,lastPassed=0,pulse=0;
 const status=t=>$('audio-status').textContent=t;
 function clear(){
  ready=false;calibration=null;snapshot={db:-100,level:0};
  sim.audioReady=false;sim.controlVoice=0;envelope.reset();sim.cancelInput();
 }
 const mic=new MicrophoneInput(()=>{clear();disarm();status('麦克风中断，旅程暂停。重新开启后再校准。');$('mic-toggle').textContent='重新开启麦克风';$('audio-calibrate').disabled=true;});
 const serial=new SerialOutput(navigator.serial,t=>{$('physical-status').textContent=t;$('hardware-arm').disabled=!serial.connected;$('serial-toggle').textContent=serial.connected?'断开 Arduino':'连接 Arduino';});
 function canSample(){
  const track=mic.stream?.getAudioTracks()[0];
  return mic.active&&mic.context?.state==='running'&&track?.readyState==='live'&&!track.muted;
 }
 function disarm(){serial.setArmed(false);$('hardware-arm').checked=false;}
 function beginCalibration(){clear();calibration={phase:'noise',time:0,noise:[],voice:[]};disarm();status('校准 1/2：保持安静 2 秒。');}
 $('mic-toggle').onclick=async()=>{
  if(pending)return;
  if(mic.active){mic.stop();clear();calibration=null;disarm();status('麦克风已关闭，旅程暂停。');$('mic-toggle').textContent='开启麦克风并校准';$('audio-calibrate').disabled=true;return;}
  pending=true;$('mic-toggle').disabled=true;
  try{if(await mic.start()){$('mic-toggle').textContent='关闭麦克风';$('audio-calibrate').disabled=false;beginCalibration();}}
  catch(e){clear();status('无法开启麦克风：'+e.message+'。允许权限后重试。');}
  finally{pending=false;$('mic-toggle').disabled=false;}
 };
 $('audio-calibrate').onclick=()=>{
  if(!mic.active)return;
  if(canSample())beginCalibration();
  else{mic.stop();$('mic-toggle').click();}
 };
 $('serial-toggle').onclick=async()=>{try{if(serial.connected)await serial.disconnect();else await serial.connect();}catch(e){$('physical-status').textContent='未连接：'+e.message;}};
 $('hardware-arm').onchange=()=>{serial.setArmed($('hardware-arm').checked&&!isPaused());$('hardware-arm').checked=serial.armed;};
 $('hardware-stop').onclick=disarm;
 const timer=setInterval(()=>{
  const active=performance.now()-lastFrame<300&&!document.hidden&&!isPaused(),out=active?levels:OUTPUT_ZERO;
  serial.send(out,performance.now());$('volume-meter').value=snapshot.level;
  $('volume-db').textContent=snapshot.db.toFixed(1)+' dBFS';$('volume-speed').textContent=Math.round(snapshot.level*100)+'%';
  $('fan-preview').textContent='风力 '+Math.round(out.fan*100)+'%';$('light-preview').textContent='灯光 '+Math.round(out.light*100)+'%';
  window.dispatchEvent(new CustomEvent('echoes:outputs',{detail:{version:2,preview:out,armed:serial.armed,connected:serial.connected,simulated:false}}));
 },100);
 document.addEventListener('visibilitychange',()=>{
  if(!document.hidden)return;
  disarm();
  if(calibration){
   clear();
   status('因离开页面暂停了声音校准。回来后重新校准。');
  }else{
   snapshot={db:-100,level:0};
   sim.audioReady=false;sim.controlVoice=0;envelope.reset();
  }
 });
 window.addEventListener('pagehide',()=>{clearInterval(timer);mic.stop();disarm();void serial.disconnect();});
 return {
  beforeUpdate(dt,now){
   lastFrame=now;
   if(document.hidden)return;
   const canMeasure=canSample();
   if(!canMeasure&&(calibration||ready)){
    clear();disarm();
    if(mic.active)status('没有收到麦克风信号。请重新校准，或关闭后重新开启麦克风。');
   }
   const db=canMeasure?mic.readDb():-100;
   if(calibration&&canMeasure){
    calibration.time+=dt;calibration[calibration.phase].push(db);
    if(calibration.phase==='noise'&&calibration.time>=2){calibration.phase='voice';calibration.time=0;status('校准 2/2：舒服地哼声 3 秒，不需要喊叫。');}
    else if(calibration.phase==='voice'&&calibration.time>=3){
     const settings=comfortCalibration(calibration.noise,calibration.voice);calibration=null;
     if(settings){envelope.configure(settings);envelope.reset();ready=true;status('声音已就绪。轻声在左侧，舒适发声在右侧；需要时可重新校准。');}
     else{ready=false;status('未能建立声音范围。安静时避免背景声，哼声时靠近麦克风，再试一次。');}
    }
   }
   snapshot=canMeasure&&ready?envelope.update(db,dt):{db,level:0};
   sim.audioReady=canMeasure&&ready;
   sim.controlVoice=sim.audioReady?snapshot.level:0;sim.trailStrength=1.5;
  },
  setupPrompt(){
   const detail=$('audio-status').textContent;
   if(pending)return {title:'正在开启麦克风',detail:'请在浏览器的权限提示中选择允许。'};
   if(calibration){
    const quiet=calibration.phase==='noise';
    const remaining=Math.max(1,Math.ceil((quiet?2:3)-calibration.time));
    return {
     title:quiet?'保持安静':'开始轻轻哼声',
     detail:quiet?`保持安静 ${remaining} 秒，然后会提示你哼声。`:`再舒服地哼声 ${remaining} 秒，不需要喊叫。`,
    };
   }
   if(mic.active)return canSample()
    ? {title:'重新校准麦克风',detail,button:'重新校准',action:()=>$('audio-calibrate').click()}
    : {title:'等待麦克风信号',detail,button:'重新连接麦克风',action:()=>$('audio-calibrate').click()};
   return {title:'先开启并校准麦克风',detail,button:'开启麦克风',action:()=>$('mic-toggle').click()};
  },
  afterUpdate(dt){
   if(isPaused()&&serial.armed)disarm();
   if(sim.passed>lastPassed)pulse=1;else pulse=Math.max(0,pulse-dt*.6);lastPassed=sim.passed;
   levels=outputLevels({active:sim.started&&!sim.complete&&!isPaused(),speed:sim.speedMultiplier,glow:sim.glow,pulse,scene:sim.level.id});
  }
 };
}
