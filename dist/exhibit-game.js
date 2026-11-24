import {ExhibitSerial, OFF} from './exhibit-serial.js';

// A touch makes the motion and its physical echo happen together.
// Use the same low current colors as the UNO-powered bench test.
const COLORS = {touchA: [12, 4, 0], touchB: [0, 3, 12]};
const ACTIONS = {touchA: 'A · 点击，灯 A 暖色', touchB: 'B · 点击，灯 B 蓝色'};
const MIN_ON_MS = 500;
const MAX_ON_MS = 1800;
const FADE_MS = 420;

export class TouchLampCue {
  start(source, now) {
    this.cue = {source, started: now, offAt: now + MAX_ON_MS};
  }

  release(source, now) {
    if (this.cue?.source !== source) return;
    const {started, offAt} = this.cue;
    this.cue.offAt = Math.min(offAt, Math.max(started + MIN_ON_MS, now));
  }

  clear() {
    this.cue = null;
  }

  output(now) {
    if (!this.cue) return OFF;
    const {source, offAt} = this.cue;
    const strength = Math.min(1, 1 - (now - offAt) / FADE_MS);
    if (strength <= 0) {
      this.clear();
      return OFF;
    }
    const color = COLORS[source].map(channel => Math.round(channel * strength));
    return {...OFF, [source === 'touchA' ? 'lampA' : 'lampB']: color};
  }
}

export function installExhibitGame(panel, {respondToTouch, isBlocked}) {
  const serial = new ExhibitSerial();
  const cue = new TouchLampCue();
  panel.innerHTML = `
    <h3>展台触摸联动</h3>
    <p class="level-help">点击传感器尚未接入时，用空格代替一次点击。接入后，触摸 A 或 B 的按下沿接到同一个点击动作。握拳与张手仍由摄像头识别。</p>
    <div class="sensor-readout"><span id="game-touch-a">A · —</span><span id="game-touch-b">B · —</span></div>
    <button id="game-touch-connect" class="quiet full" type="button">连接 Arduino · 触摸联动</button>
    <p id="game-touch-status" class="level-help" role="status">未连接</p>
    <label class="toggle-line"><span>启用真实灯光</span><input id="game-touch-arm" type="checkbox" disabled></label>
    <output id="game-touch-action" class="level-help" role="status">等待触摸</output>
  `;
  const ui = id => panel.querySelector('#game-touch-' + id);
  const clear = () => {
    cue.clear();
    serial.setOutputs(OFF);
    serial.flush();
  };
  const inputText = value => value === undefined ? '—' : value === null ? '未启用' : value ? '按下' : '松开';

  serial.addEventListener('status', ({detail}) => {
    ui('status').textContent = detail;
    ui('connect').textContent = serial.port ? '断开 Arduino' : '连接 Arduino · 触摸联动';
    ui('arm').disabled = !serial.ready;
  });
  serial.addEventListener('armed', ({detail}) => {
    ui('arm').checked = detail;
    if (!detail) cue.clear();
  });
  serial.addEventListener('inputs', ({detail}) => {
    ui('a').textContent = 'A · ' + inputText(detail?.touchA);
    ui('b').textContent = 'B · ' + inputText(detail?.touchB);
  });
  serial.addEventListener('touch', ({detail: {source, pressed}}) => {
    if (!(source in ACTIONS)) return;
    const now = performance.now();
    if (!pressed) {
      cue.release(source, now);
      return;
    }
    if (document.hidden || isBlocked() || !respondToTouch()) return;
    ui('action').textContent = ACTIONS[source] + (serial.armed ? '' : ' · 灯光未启用');
    if (serial.armed) {
      cue.start(source, now);
      serial.setOutputs(cue.output(now));
      serial.flush();
    }
  });
  ui('connect').onclick = async () => {
    ui('connect').disabled = true;
    try {
      if (serial.port) await serial.disconnect();
      else await serial.connect();
    } catch (error) {
      ui('status').textContent = error.name === 'NotFoundError' ? '未选择串口' : '连接失败：' + error.message;
    } finally {
      ui('connect').disabled = false;
    }
  };
  ui('arm').onchange = () => {
    if (ui('arm').checked && (document.hidden || isBlocked())) {
      ui('arm').checked = false;
      ui('status').textContent = '继续游戏后再启用灯光';
      return;
    }
    serial.setArmed(ui('arm').checked);
    if (serial.armed) ui('status').textContent = '灯光已启用 · 按触摸开关测试';
    else if (serial.ready) ui('status').textContent = '仍可用触摸控制角色';
  };
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) serial.setArmed(false);
  });
  window.addEventListener('pagehide', () => void serial.disconnect());

  return {
    clear,
    update(now) {
      if (!serial.armed) return;
      if (isBlocked()) {
        serial.setArmed(false);
        return;
      }
      serial.setOutputs(cue.output(now));
    },
  };
}
