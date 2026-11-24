import {ExhibitSerial, OFF} from './exhibit-serial.js';

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
  panel.innerHTML = `
    <h3>展台触摸联动</h3>
    <p class="level-help">网页 A/B 可直接选择。接好 Arduino 后，实体 A 按下亮暖灯，实体 B 按下亮蓝灯。启用实体灯光后，网页触摸也会点亮对应灯。</p>
    <div class="sensor-readout"><span id="game-touch-a">A · —</span><span id="game-touch-b">B · —</span></div>
    <button id="game-touch-connect" class="quiet full" type="button">连接 Arduino · 触摸联动</button>
    <p id="game-touch-status" class="level-help" role="status">未连接 Arduino</p>
    <label class="toggle-line"><span>启用实体灯光</span><input id="game-touch-arm" type="checkbox" disabled></label>
    <output id="game-touch-action" class="level-help" role="status">等待触摸</output>
    <p class="level-help"><a href="exhibit-hardware.html">独立测试两灯和舵机输出</a></p>
  `;
  const ui = id => panel.querySelector('#game-touch-' + id);
  const buttons = Array.from(choices.querySelectorAll('[data-touch-option]'));
  const inputText = value => value === undefined ? '—' : value === null ? '未启用' : value ? '按下' : '松开';
  const canPlay = () => !document.hidden && !isBlocked();
  function press(source, momentary = false) {
    if (!canPlay()) return;
    const index = SOURCES.indexOf(source);
    const label = buttons[index].textContent.trim();
    if (!respondToTouch(index)) return;
    ui('action').textContent = label + (serial.armed ? ' · 实体灯光联动' : ' · 网页已响应');
    if (serial.armed) {
      const now = performance.now();
      cue.start(source, now);
      if (momentary) cue.release(source, now);
      serial.setOutputs(cue.output(now));
      serial.flush();
    }
  }
  buttons.forEach((button, index) => {
    button.onclick = () => press(SOURCES[index], true);
  });

  serial.addEventListener('status', ({detail}) => {
    ui('status').textContent = serial.ready && serial.inputs?.touchB === null
      ? 'EOL3 已连接 · 固件未启用 B，请更新展台固件'
      : detail;
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
      ui('status').textContent = error.name === 'NotFoundError' ? '未选择串口' : '连接失败：' + error.message;
    } finally {
      ui('connect').disabled = false;
    }
  };
  ui('arm').onchange = () => {
    if (ui('arm').checked && !canPlay()) {
      ui('arm').checked = false;
      ui('status').textContent = '继续游戏后再启用灯光';
      return;
    }
    serial.setArmed(ui('arm').checked);
    if (serial.armed) ui('status').textContent = '实体灯光已启用';
    else if (serial.ready) ui('status').textContent = '灯光已停止 · 两路触摸仍可控制';
  };
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) serial.setArmed(false);
  });
  window.addEventListener('pagehide', () => void serial.disconnect());

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
    update(now) {
      if (!serial.armed) return;
      if (!canPlay()) {
        serial.setArmed(false);
        if (serial.ready) ui('status').textContent = '旅程暂停 · 实体灯光已停止';
        return;
      }
      serial.setOutputs(cue.output(now));
    },
  };
}
