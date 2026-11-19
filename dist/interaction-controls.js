import {VOLUME_DEFAULTS,volumeSettings,VolumeEnvelope,ambientCalibration} from './volume-core.js';
import {MicrophoneInput} from './audio-input.js';
import {SerialOutput,outputLevels,OUTPUT_ZERO} from './physical-output.js';

export function installInteractionControls(sim,isPaused){
  const panel=document.createElement('section');panel.className='control-section interaction-controls';
  panel.innerHTML=`<h3>03 <span>声音推动旅程</span></h3>
    <p class="level-help">轻声慢行，大声加速；手势继续控制跃起与能力。安静时逐渐停下。</p>
    <button id="mic-toggle" class="primary full">开启麦克风</button>
    <p id="audio-status" class="level-help" role="status">未开启 · 使用原有速度</p>
    <meter id="volume-meter" min="0" max="1" value="0" aria-label="声音能量"></meter>
    <div class="sensor-readout"><span id="volume-db">−100 dBFS</span><strong id="volume-speed">1.00×</strong></div>
    <button id="audio-calibrate" class="quiet full" disabled>校准环境噪声 · 安静 2 秒</button>
    <details><summary>声音与光迹调试</summary><p class="level-help">先校准安静阈值，再正常发声；降低满速阈值可更容易加速。参数保存在本机。</p>
      <label>安静阈值 <output id="quietDb-value"></output><input id="quietDb" type="range" min="-75" max="-15" step="1"></label>
      <label>满速阈值 <output id="loudDb-value"></output><input id="loudDb" type="range" min="-60" max="-3" step="1"></label>
      <label>最高速度 <output id="maxSpeed-value"></output><input id="maxSpeed" type="range" min="0.5" max="3" step="0.1"></label>
      <label>加速平滑 / 秒 <output id="attack-value"></output><input id="attack" type="range" min="0.05" max="1" step="0.05"></label>
      <label>减速平滑 / 秒 <output id="release-value"></output><input id="release" type="range" min="0.1" max="2" step="0.05"></label>
      <label>光迹强度 <output id="trail-value"></output><input id="trail" type="range" min="0.5" max="3" step="0.1"></label>
      <label class="toggle-line"><span>模拟音量（仅屏幕测试）</span><input id="audio-demo" type="checkbox"></label>
      <label>模拟输入 / dBFS <output id="demo-db-value">−35</output><input id="demo-db" type="range" min="-90" max="0" value="-35" disabled></label>
      <button id="audio-defaults" class="quiet full">恢复默认参数</button>
    </details>
    <h3 class="output-heading">04 <span>风与灯光</span></h3>
    <div class="sensor-readout"><span id="fan-preview">风力 0%</span><span id="light-preview">灯光 0%</span><i id="lamp-preview"></i></div>
    <p id="physical-status" class="level-help" role="status">仅预览 · 尚未连接 Arduino</p>
    <details><summary>硬件接口调试</summary>
      <p class="level-help">速度带动风力，收集与手势点亮灯光。连接后需手动启用输出。</p>
      <button id="serial-toggle" class="quiet full">连接 Arduino</button>
      <label class="toggle-line"><span>启用实际输出</span><input id="hardware-arm" type="checkbox" disabled></label>
      <label>风力上限 <output id="fan-limit-value">65%</output><input id="fan-limit" type="range" min="0" max="100" value="65"></label>
      <label>灯光上限 <output id="light-limit-value">50%</output><input id="light-limit" type="range" min="0" max="100" value="50"></label>
      <button id="hardware-stop" class="full stop-output">停止硬件输出</button>
      <code id="physical-packet">等待输出帧</code>
    </details>`;
  document.querySelector('aside .session').before(panel);
  const $=id=>panel.querySelector('#'+id);
  let settings;try{settings=volumeSettings(JSON.parse(localStorage.getItem('echoes-volume-v1')||'{}'));}catch{settings=volumeSettings();}
  const envelope=new VolumeEnvelope(settings);
  let snapshot={db:-100,level:0,speed:1},calibration=null,audioPending=false,pulse=0,lastPassed=sim.passed,lastTime=sim.time,lastFrame=-Infinity;
  let levels={...OUTPUT_ZERO};
  function status(text){$('audio-status').textContent=text;}
  const mic=new MicrophoneInput(()=>{envelope.reset();status('麦克风已断开 · 恢复原有速度');$('mic-toggle').textContent='开启麦克风';$('audio-calibrate').disabled=true;calibration=null;disarm();});
  const serial=new SerialOutput(navigator.serial,text=>{$('physical-status').textContent=text;$('serial-toggle').textContent=serial.connected?'断开 Arduino':'连接 Arduino';$('hardware-arm').disabled=!serial.connected||$('audio-demo').checked;$('hardware-arm').checked=serial.armed;});
  function disarm(){serial.setArmed(false);$('hardware-arm').checked=false;}
  function updateSettings(save=true){
    envelope.configure(settings);for(const key of Object.keys(VOLUME_DEFAULTS)){$(key).value=settings[key];$(key+'-value').textContent=settings[key]+(key.endsWith('Db')?' dBFS':key==='maxSpeed'?'×':'');}
    sim.trailStrength=settings.trail;
    if(save)try{localStorage.setItem('echoes-volume-v1',JSON.stringify(settings));}catch{}
  }
  updateSettings(false);
  for(const key of Object.keys(VOLUME_DEFAULTS))$(key).oninput=()=>{settings=volumeSettings({...settings,[key]:Number($(key).value)});updateSettings();};
  $('audio-defaults').onclick=()=>{settings={...VOLUME_DEFAULTS};updateSettings();};
  $('mic-toggle').onclick=async()=>{
    if(audioPending)return;
    if(mic.active){mic.stop();envelope.reset();calibration=null;disarm();$('audio-calibrate').disabled=true;$('mic-toggle').textContent='开启麦克风';status('已关闭 · 恢复原有速度');return;}
    audioPending=true;$('mic-toggle').disabled=true;status('等待麦克风权限…');
    try{if(await mic.start()){$('audio-demo').checked=false;$('demo-db').disabled=true;envelope.reset();$('mic-toggle').textContent='关闭麦克风';$('audio-calibrate').disabled=false;$('hardware-arm').disabled=!serial.connected;status('音量控制已开启 · 建议先校准环境噪声');}}
    catch(error){status('无法开启麦克风：'+error.message);}
    finally{audioPending=false;$('mic-toggle').disabled=false;}
  };
  $('audio-calibrate').onclick=()=>{if(!mic.active)return;calibration={start:performance.now(),samples:[]};disarm();envelope.reset();status('请保持安静，正在测量环境噪声…');};
  $('audio-demo').onchange=()=>{
    disarm();mic.stop();envelope.reset();calibration=null;$('audio-calibrate').disabled=true;$('mic-toggle').textContent='开启麦克风';
    $('demo-db').disabled=!$('audio-demo').checked;$('hardware-arm').disabled=!serial.connected||$('audio-demo').checked;
    status($('audio-demo').checked?'模拟音量 · 不采集声音、不发送非零硬件输出':'模拟已关闭 · 使用原有速度');
  };
  $('demo-db').oninput=()=>$('demo-db-value').textContent=$('demo-db').value;
  $('serial-toggle').onclick=async()=>{
    $('serial-toggle').disabled=true;
    try{if(serial.connected)await serial.disconnect();else await serial.connect();}
    catch(error){$('physical-status').textContent=error.name==='NotFoundError'?'未选择串口 · 仅预览':'连接失败：'+error.message;}
    finally{$('serial-toggle').disabled=false;}
  };
  $('hardware-arm').onchange=()=>{serial.setArmed($('hardware-arm').checked&&!$('audio-demo').checked&&!document.hidden);$('hardware-arm').checked=serial.armed;$('physical-status').textContent=serial.armed?'实际输出已启用 · 串口已发送不等于硬件已响应':'实际输出已关闭 · 仅预览';};
  $('hardware-stop').onclick=()=>{disarm();$('physical-status').textContent='输出已停止 · 重新勾选后才会恢复';};
  for(const key of ['fan','light'])$(key+'-limit').oninput=()=>$(key+'-limit-value').textContent=$(key+'-limit').value+'%';
  const timer=setInterval(()=>{
    const fresh=performance.now()-lastFrame<300&&!document.hidden&&!isPaused()&&!calibration;
    const preview=fresh?levels:{...OUTPUT_ZERO};
    serial.send(fresh&&!$('audio-demo').checked?preview:OUTPUT_ZERO,performance.now());
    $('volume-meter').value=snapshot.level;$('volume-db').textContent=snapshot.db.toFixed(1)+' dBFS';$('volume-speed').textContent=(fresh?sim.speedMultiplier:0).toFixed(2)+'×';
    $('fan-preview').textContent='风力 '+Math.round(preview.fan*100)+'%';$('light-preview').textContent='灯光 '+Math.round(preview.light*100)+'%';
    $('lamp-preview').style.background=`rgb(${preview.rgb.join(',')})`;$('lamp-preview').style.opacity=preview.light;
    $('physical-packet').textContent=serial.lastPacket||'未发送 · 仅预览';
    window.dispatchEvent(new CustomEvent('echoes:outputs',{detail:{version:2,preview,armed:serial.armed,connected:serial.connected,simulated:$('audio-demo').checked}}));
  },100);
  document.addEventListener('visibilitychange',()=>{if(document.hidden)disarm();});
  window.addEventListener('pagehide',()=>{clearInterval(timer);mic.stop();disarm();void serial.disconnect();});
  return {
    beforeUpdate(dt,now){
      lastFrame=now;const enabled=mic.active||$('audio-demo').checked,db=$('audio-demo').checked?Number($('demo-db').value):mic.readDb();
      if(calibration){calibration.samples.push(db);if(now-calibration.start>=2000){settings=ambientCalibration(calibration.samples,settings);updateSettings();calibration=null;status('环境校准完成 · 请发声测试速度');}}
      snapshot=enabled?envelope.update(calibration?-100:db,dt):{db:-100,level:0,speed:1};
      sim.speedMultiplier=calibration?0:snapshot.speed;sim.soundEnergy=enabled?snapshot.level:0;sim.trailStrength=settings.trail;
      if(enabled&&!isPaused()&&!calibration&&snapshot.level>.025)sim.started=true;
    },
    afterUpdate(dt){
      if(sim.time<lastTime||sim.passed<lastPassed)pulse=0;
      if(sim.passed>lastPassed)pulse=1;else pulse=Math.max(0,pulse-dt*.5);
      lastTime=sim.time;lastPassed=sim.passed;
      levels=outputLevels({active:sim.started&&!sim.complete&&!isPaused()&&!calibration&&sim.recovery<=0,speed:sim.speedMultiplier,glow:sim.glow,pulse,scene:sim.level.id,fanLimit:Number($('fan-limit').value)/100,lightLimit:Number($('light-limit').value)/100});
    }
  };
}
