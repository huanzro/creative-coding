import {VolumeEnvelope,volumeSettings,ambientCalibration} from './volume-core.js';
import {MicrophoneInput} from './audio-input.js';
import {outputLevels} from './physical-output.js';
import {loadInputProfile,saveInputProfile} from './input-profiles.js';

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
 panel.innerHTML=`<h3>02 <span>真实声音</span></h3><p class="level-help">首次使用这支麦克风时，先安静 2 秒，再舒适地哼声 3 秒。下次开启会沿用本机设置。</p><button id="mic-toggle" class="primary full">开启麦克风</button><p id="audio-status" class="level-help" role="status">麦克风未开启。</p><meter id="volume-meter" min="0" max="1" value="0" aria-label="真实麦克风声音能量"></meter><div class="sensor-readout"><span id="volume-db">— dBFS</span><strong id="volume-speed">0%</strong></div><button id="audio-calibrate" class="quiet full" disabled>重新校准我的声音</button>`;
 document.querySelector('aside .control-section').after(panel);
 const $=id=>panel.querySelector('#'+id),envelope=new VolumeEnvelope();
 let calibration=null,ready=false,pending=false,snapshot={db:-100,level:0},lastPassed=0,pulse=0;
 let missingSince=null;
 const status=t=>$('audio-status').textContent=t;
 function clear(){
  ready=false;calibration=null;missingSince=null;snapshot={db:-100,level:0};
  sim.audioReady=false;sim.controlVoice=0;envelope.reset();sim.cancelInput();
 }
 const mic=new MicrophoneInput(()=>{clear();status('麦克风已断开，请重新开启。');$('mic-toggle').textContent='重新开启麦克风';$('audio-calibrate').disabled=true;});
 const audioTrack=()=>mic.stream?.getAudioTracks()[0];
 function canSample(){
  const track=audioTrack();
  return mic.active&&mic.context?.state==='running'&&track?.readyState==='live'&&!track.muted;
 }
 function beginCalibration(){clear();calibration={phase:'noise',time:0,noise:[],voice:[]};status('校准 1/2：保持安静 2 秒。');}
 function applySettings(settings,detail){
  envelope.configure(settings);envelope.reset();ready=true;status(detail);
 }
 $('mic-toggle').onclick=async()=>{
  if(pending)return;
  if(mic.active){mic.stop();clear();status('麦克风已关闭，旅程暂停。');$('mic-toggle').textContent='开启麦克风';$('audio-calibrate').disabled=true;return;}
  pending=true;$('mic-toggle').disabled=true;
  try{
   if(await mic.start()){
    $('mic-toggle').textContent='关闭麦克风';$('audio-calibrate').disabled=false;
    const saved=loadInputProfile('mic',audioTrack());
    if(Number.isFinite(saved?.quietDb)&&Number.isFinite(saved?.loudDb)&&saved.loudDb-saved.quietDb>=6){
     clear();applySettings(saved,'声音已就绪，已沿用上次设置。');
    }else beginCalibration();
   }
  }
  catch(e){clear();status('无法开启麦克风：'+e.message+'。允许权限后重试。');}
  finally{pending=false;$('mic-toggle').disabled=false;}
 };
 $('audio-calibrate').onclick=()=>{
  if(!mic.active)return;
  if(canSample())beginCalibration();
  else{mic.stop();$('mic-toggle').click();}
 };
 const timer=setInterval(()=>{
  $('volume-meter').value=snapshot.level;
  $('volume-db').textContent=snapshot.db.toFixed(1)+' dBFS';$('volume-speed').textContent=Math.round(snapshot.level*100)+'%';
 },100);
 document.addEventListener('visibilitychange',()=>{
  if(!document.hidden)return;
  snapshot={db:-100,level:0};
  sim.audioReady=false;sim.controlVoice=0;envelope.reset();
  missingSince=null;
 });
 window.addEventListener('pagehide',()=>{clearInterval(timer);mic.stop();});
 return {
  beforeUpdate(dt,now){
   if(document.hidden)return;
   const canMeasure=canSample();
   if(canMeasure){
    if(ready&&missingSince!==null&&!sim.audioReady)status('声音已恢复，继续使用原设置。');
    missingSince=null;
   }else if(mic.active){
    missingSince??=now;
   }
   const interrupted=missingSince!==null&&now-missingSince>=1000;
   if(interrupted&&(calibration||sim.audioReady)){
    calibration=null;
    status('暂时没有收到麦克风信号。可等待恢复或重新连接麦克风。');
   }
   const db=canMeasure?mic.readDb():-100;
   if(calibration&&canMeasure){
    calibration.time+=dt;calibration[calibration.phase].push(db);
    if(calibration.phase==='noise'&&calibration.time>=2){calibration.phase='voice';calibration.time=0;status('校准 2/2：舒服地哼声 3 秒，不需要喊叫。');}
    else if(calibration.phase==='voice'&&calibration.time>=3){
     const settings=comfortCalibration(calibration.noise,calibration.voice);calibration=null;
     if(settings){
      saveInputProfile('mic',audioTrack(),settings);
      applySettings(settings,'声音已就绪，设置已保存到本机。');
     }
     else{ready=false;status('未能建立声音范围。安静时避免背景声，哼声时靠近麦克风，再试一次。');}
    }
   }
   snapshot=ready?envelope.update(db,dt):{db,level:0};
   sim.audioReady=ready&&mic.active&&!interrupted;
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
   return {title:'先开启麦克风',detail,button:'开启麦克风',action:()=>$('mic-toggle').click()};
  },
  afterUpdate(dt){
   if(sim.passed>lastPassed)pulse=1;else pulse=Math.max(0,pulse-dt*.6);lastPassed=sim.passed;
   return outputLevels({active:sim.started&&!sim.complete&&!isPaused(),speed:sim.speedMultiplier,glow:sim.glow,pulse,scene:sim.level.id});
  }
 };
}
