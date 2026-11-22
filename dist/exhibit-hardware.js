import {ExhibitSerial, TOUCH_CHANNELS} from './exhibit-serial.js';

const ui = id => document.getElementById(id);
const exhibit = new ExhibitSerial();
const labels = {touchA: '稳定触摸 A', touchB: '稳定触摸 B', fabric: '导电布'};
let connected = false;

function reflectTouch({source, pressed}, simulated = false) {
  ui('edge').textContent = `${simulated ? '模拟 · ' : ''}${labels[source]}${pressed ? '按下' : '松开'}`;
}

exhibit.addEventListener('inputs', ({detail}) => {
  for (const name of TOUCH_CHANNELS) {
    const value = detail?.[name];
    ui(name).textContent = value === undefined ? '—' : value === null ? '未启用' : value ? '按下' : '松开';
  }
});
exhibit.addEventListener('touch', ({detail}) => reflectTouch(detail));
exhibit.addEventListener('ack', ({detail}) => {
  ui('ack').textContent = `Arduino 已收包 #${detail} · 仍需观察灯和舵机`;
});
exhibit.addEventListener('status', ({detail}) => {
  ui('status').textContent = detail;
  connected = Boolean(exhibit.port) && detail !== '未连接';
  ui('connect').disabled = connected;
  ui('disconnect').disabled = !connected;
  ui('arm').disabled = !exhibit.ready;
  if (!connected) ui('ack').textContent = '等待 Arduino 回执';
});
exhibit.addEventListener('armed', ({detail}) => {
  ui('arm').checked = detail;
  ui('apply').disabled = !detail;
});

ui('connect').onclick = async () => {
  ui('connect').disabled = true;
  try { await exhibit.connect(); }
  catch (error) {
    ui('status').textContent = error.name === 'NotFoundError' ? '已取消选择串口' : error.message;
    ui('connect').disabled = false;
  }
};
ui('disconnect').onclick = () => exhibit.disconnect();
ui('stop').onclick = () => exhibit.setArmed(false);
ui('arm').onchange = event => exhibit.setArmed(event.target.checked);
document.addEventListener('visibilitychange', () => { if (document.hidden) exhibit.setArmed(false); });
window.addEventListener('pagehide', () => void exhibit.disconnect());

function rgb(hex, scale) {
  return [1, 3, 5].map(i => Math.round(parseInt(hex.slice(i, i + 2), 16) * scale));
}
function paintPreview() {
  const brightness = Number(ui('brightness').value);
  ui('percent').textContent = brightness + '%';
  for (const strip of ['a', 'b']) {
    ui('preview-' + strip).style.background = `rgb(${rgb(ui('lamp-' + strip).value, brightness / 100).join(',')})`;
  }
}
for (const id of ['lamp-a', 'lamp-b', 'brightness']) ui(id).oninput = paintPreview;
paintPreview();
ui('apply').onclick = () => {
  const scale = Number(ui('brightness').value) / 100;
  exhibit.setOutputs({
    lampA: rgb(ui('lamp-a').value, scale),
    lampB: rgb(ui('lamp-b').value, scale),
    servo: ui('servo').value === '' ? null : Number(ui('servo').value),
  });
  exhibit.flush();
};
const simulate = pressed => reflectTouch({source: 'touchA', pressed}, true);
ui('simulate').onpointerdown = event => {
  event.currentTarget.setPointerCapture(event.pointerId);
  simulate(true);
};
ui('simulate').onpointerup = ui('simulate').onpointercancel = () => simulate(false);
ui('simulate').onclick = event => { if (event.detail === 0) simulate(false); };
window.addEventListener('keydown', event => {
  if (event.code === 'KeyT' && !event.repeat && !event.ctrlKey && !event.altKey && !event.metaKey) simulate(true);
});
window.addEventListener('keyup', event => { if (event.code === 'KeyT') simulate(false); });
window.addEventListener('blur', () => {
  if (ui('edge').textContent === '模拟 · 稳定触摸 A按下') simulate(false);
});
