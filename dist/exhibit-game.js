import {ExhibitSerial, OFF} from './exhibit-serial.js';
import {OUTPUT_ZERO} from './physical-output.js';

// A touch makes the motion and its physical echo happen together.
// Use the same low current colors as the UNO-powered bench test.
const COLORS = {touchA: [12, 4, 0], touchB: [0, 3, 12]};
const SOURCES = ['touchA', 'touchB'];
const MIN_ON_MS = 500;
const MAX_ON_MS = 1800;
const FADE_MS = 420;

export class TouchLampCue {
  cues = new Map();

  start(source, now) {
    if (Object.hasOwn(COLORS, source)) this.cues.set(source, {started: now, offAt: now + MAX_ON_MS});
  }

  release(source, now) {
    const cue = this.cues.get(source);
    if (cue) cue.offAt = Math.min(cue.offAt, Math.max(cue.started + MIN_ON_MS, now));
  }

  clear() {
    this.cues.clear();
  }

  output(now) {
    if (!this.cues.size) return OFF;
    const lamps = {...OFF};
    for (const [source, {offAt}] of this.cues) {
      const strength = Math.min(1, 1 - (now - offAt) / FADE_MS);
      if (strength <= 0) {
        this.cues.delete(source);
        continue;
      }
      lamps[source === 'touchA' ? 'lampA' : 'lampB'] = COLORS[source].map(value => Math.round(value * strength));
    }
    return this.cues.size ? lamps : OFF;
  }
}

export function installExhibitGame(panel, choices, {respondToTouch, isBlocked}) {
  const serial = new ExhibitSerial();
  const cue = new TouchLampCue();
  let levels = OUTPUT_ZERO;
  let lastFrame = -Infinity;
  panel.innerHTML = `
    <h3>展台硬件</h3>
    <p class="level-help">触摸、灯光和风扇共用一次 Arduino 连接。按实体或网页 A 点亮暖灯，按 B 点亮蓝灯。</p>
    <div class="sensor-readout"><span id="game-touch-a">A · —</span><span id="game-touch-b">B · —</span></div>
    <button id="game-touch-connect" class="primary full" type="button">连接 Arduino</button>
    <p id="game-touch-status" class="level-help" role="status">未连接 Arduino</p>
    <label class="toggle-line"><span>启用实体灯光</span><input id="game-touch-arm" type="checkbox" disabled></label>
    <label class="toggle-line"><span>灯光跟随场景</span><input id="game-touch-scene" type="checkbox" disabled></label>
    <label class="toggle-line"><span>启用风扇输出</span><input id="game-touch-fan" type="checkbox" disabled></label>
    <p id="game-touch-fan-status" class="level-help">风扇需使用组合固件。</p>
    <div class="sensor-readout"><span id="fan-preview">风力 0%</span><span id="light-preview">灯光 0%</span></div>
    <button id="game-touch-stop" class="quiet full" type="button" disabled>停止实体输出</button>
    <output id="game-touch-action" class="level-help" role="status">等待触摸</output>
    <p class="level-help"><a href="exhibit-hardware.html">独立硬件调试</a>（使用前先断开游戏串口）</p>
  `;
  const ui = id => panel.querySelector('#game-touch-' + id);
  const buttons = Array.from(choices.querySelectorAll('[data-touch-option]'));
  const inputText = value => value === undefined ? '—' : value === null ? '未启用' : value ? '按下' : '松开';
  const canPlay = () => !document.hidden && !isBlocked();
  function refreshControls(){
    ui('arm').disabled = !serial.ready;
    ui('scene').disabled = !serial.ready || !ui('arm').checked;
    ui('fan').disabled = !serial.ready || !serial.fanSupported;
    ui('stop').disabled = !serial.armed;
    ui('fan-status').textContent = serial.fanSupported
      ? '组合固件已就绪，风扇随旅程速度变化。'
      : '要使用风扇，请上传组合固件。旧 EOL3 固件可继续使用触摸和灯光。';
  }
  function updateOutputs(now, send=false){
    if(!serial.armed)return;
    const touch = ui('arm').checked ? cue.output(now) : OFF;
    const sceneLight = ui('arm').checked && ui('scene').checked
      ? levels.rgb.map(value => Math.round(value / 255 * levels.light * 16))
      : OFF.lampA;
    const lampWithScene = rgb => rgb.map((value,index)=>Math.max(value,sceneLight[index]));
    serial.setOutputs({
      lampA:lampWithScene(touch.lampA),lampB:lampWithScene(touch.lampB),servo:null,
      fan:ui('fan').checked ? Math.round(levels.fan*255) : 0,
    });
    if(send)serial.flush();
  }
  function stopOutputs(message){
    serial.setArmed(false);
    if(serial.ready)ui('status').textContent=message;
  }
  function press(source, momentary = false) {
    if (!canPlay()) return;
    const index = SOURCES.indexOf(source);
    const label = buttons[index].textContent.trim();
    if (!respondToTouch(index)) return;
    const lighting = serial.armed && ui('arm').checked;
    ui('action').textContent = label + (lighting ? ' · 实体灯光联动' : ' · 网页已响应');
    if (lighting) {
      const now = performance.now();
      cue.start(source, now);
      if (momentary) cue.release(source, now);
      updateOutputs(now,true);
    }
  }
  buttons.forEach((button, index) => {
    button.onclick = () => press(SOURCES[index], true);
  });

  serial.addEventListener('status', ({detail}) => {
    ui('status').textContent = serial.ready && serial.inputs?.touchB === null
      ? 'EOL3 已连接 · 固件未启用 B，请更新展台固件'
      : detail;
    ui('connect').textContent = serial.port ? '断开 Arduino' : '连接 Arduino';
    refreshControls();
  });
  serial.addEventListener('fan',refreshControls);
  serial.addEventListener('armed', ({detail}) => {
    if (!detail) {
      cue.clear();
      ui('arm').checked=false;ui('fan').checked=false;ui('scene').checked=false;
    }
    refreshControls();
  });
  serial.addEventListener('inputs', ({detail}) => {
    ui('a').textContent = 'A · ' + inputText(detail?.touchA);
    ui('b').textContent = 'B · ' + inputText(detail?.touchB);
  });
  serial.addEventListener('touch', ({detail: {source, pressed}}) => {
    if (!SOURCES.includes(source)) return;
    if (pressed) press(source);
    else cue.release(source, performance.now());
  });
  ui('connect').onclick = async () => {
    ui('connect').disabled = true;
    try {
      if (serial.port) await serial.disconnect();
      else await serial.connect();
    } catch (error) {
      ui('status').textContent = error.name === 'NotFoundError' ? '未选择串口'
        : ['NetworkError','InvalidStateError'].includes(error.name)
          ? '无法打开串口。请先关闭独立调试页或 Arduino IDE 串口监视器。'
          : '连接失败：' + error.message;
    } finally {
      ui('connect').disabled = false;
    }
  };
  ui('arm').onchange = ui('fan').onchange = () => {
    const enabled = ui('arm').checked || ui('fan').checked;
    if (enabled && !canPlay()) {
      stopOutputs('继续游戏后再启用实体输出');
      return;
    }
    if(!ui('arm').checked){cue.clear();ui('scene').checked=false;}
    serial.setArmed(enabled);
    updateOutputs(performance.now(),true);
    if (serial.ready) ui('status').textContent = enabled ? '已启用选择的实体输出' : '输出已停止 · 触摸仍可控制';
  };
  ui('scene').onchange=()=>updateOutputs(performance.now(),true);
  ui('stop').onclick=()=>stopOutputs('输出已停止 · 触摸仍可控制');
  const previewTimer=setInterval(()=>{
    const current=performance.now()-lastFrame<300&&canPlay();
    if(!current&&serial.armed)stopOutputs('旅程已暂停 · 已停止实体输出');
    const out=current?levels:OUTPUT_ZERO;
    panel.querySelector('#fan-preview').textContent='风力预览 '+Math.round(out.fan*100)+'%';
    panel.querySelector('#light-preview').textContent='灯光预览 '+Math.round(out.light*100)+'%';
    window.dispatchEvent(new CustomEvent('echoes:outputs',{detail:{version:2,preview:out,armed:serial.armed,connected:serial.ready,simulated:false}}));
  },100);
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) serial.setArmed(false);
  });
  window.addEventListener('pagehide', () => {clearInterval(previewTimer);void serial.disconnect();});

  return {
    updateControls({labels, selected}, ending) {
      choices.hidden = ending;
      buttons.forEach((button, index) => {
        const label = (index === 0 ? 'A' : 'B') + ' · ' + labels[index];
        if (button.textContent !== label) button.textContent = label;
        button.disabled = ending || !canPlay();
        if (selected === undefined) button.removeAttribute('aria-pressed');
        else button.setAttribute('aria-pressed', String(selected === index));
      });
    },
    update(now,nextLevels=OUTPUT_ZERO) {
      lastFrame=now;levels=nextLevels;
      if (!serial.armed) return;
      if (!canPlay()) {
        stopOutputs('旅程已暂停 · 已停止实体输出');
        return;
      }
      updateOutputs(now);
    },
  };
}
