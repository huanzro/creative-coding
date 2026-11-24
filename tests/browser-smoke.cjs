const assert = require('node:assert/strict');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const base = new URL(process.env.ECHOES_BROWSER_TEST_ORIGIN || 'http://localhost:8776/');

async function verifySite(browser) {
  const context = await browser.newContext({viewport:{width:1440,height:980}});
  const page = await context.newPage();
  const errors = [];
  context.on('page', tab => tab.on('pageerror', error => errors.push(error.message)));
  page.on('pageerror', error => errors.push(error.message));

  await page.goto(base.href);
  await page.locator('#asset-status').waitFor({state:'hidden'});
  assert.equal(await page.locator('#scene-title').innerText(), '回声庭院');
  assert.equal(await page.locator('#pause-title').innerText(), '先开启摄像头');
  assert.equal(await page.locator('#journey-clock').innerText(), '00:00');
  assert.equal(await page.locator('#lift,#power,#audio-demo,#demo-db,#keyboard-mode').count(), 0);
  const a = page.locator('[data-touch-option="0"]');
  const b = page.locator('[data-touch-option="1"]');
  assert.equal(await a.isDisabled(), true);
  assert.equal(await b.isDisabled(), true);
  assert.match(await a.innerText(), /A · 暖光/);
  assert.match(await b.innerText(), /B · 蓝光/);

  for (const key of ['w','ArrowUp','x','e']) await page.keyboard.press(key);
  await page.keyboard.down('Space');
  await page.keyboard.down('Space');
  await page.keyboard.up('Space');
  assert.equal(Number(await page.locator('#count').innerText()), 0);
  assert.match(await page.locator('#level-progress').innerText(), /庭院/);

  const saved = {version:2,chapter:1,round:1,tutorial:null,score:300,phrase:[]};
  await page.evaluate(checkpoint => localStorage.setItem('echoes-body-v2', JSON.stringify(checkpoint)), saved);
  await page.reload();
  assert.deepEqual(await page.evaluate(() => JSON.parse(localStorage.getItem('echoes-body-v2'))), saved);
  await page.getByText('展台设置与章节', {exact:true}).click();
  await page.getByRole('button', {name:'继续上次旅程'}).click();
  assert.equal(await page.locator('#scene-title').innerText(), '回声之门');
  assert.match(await page.locator('#level-progress').innerText(), /2\/3/);
  assert.match(await a.innerText(), /A · 开始记录/);
  assert.match(await b.innerText(), /B · 开始记录/);
  await page.locator('#black-display').click();
  assert.equal(await page.locator('body.black-scene').count(), 1);
  await page.locator('#black-display').click();
  assert.match(await page.locator('#level-progress').innerText(), /2\/3/);

  const projection = page.getByRole('button', {name:/开启双屏投影/});
  const popupPromise = page.waitForEvent('popup');
  await projection.click();
  const popup = await popupPromise;
  await popup.locator('canvas').waitFor();
  await popup.waitForFunction(() => {
    const canvas = document.querySelector('canvas');
    return canvas?.width === Math.round(innerWidth * Math.min(devicePixelRatio || 1, 2));
  });
  await popup.getByRole('button', {name:'左右镜像'}).click();
  assert.equal(await popup.locator('canvas').evaluate(canvas => canvas.mirrored), true);
  await popup.keyboard.press('h');
  assert.equal(await popup.locator('body.hide').count(), 1);
  await popup.keyboard.press('Space');
  assert.equal(Number(await page.locator('#count').innerText()), 0);
  await popup.close();
  await projection.waitFor();

  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
  const overlap = await page.locator('.journey-hud').evaluate(element => {
    const hud = element.getBoundingClientRect();
    const canvas = document.getElementById('world').getBoundingClientRect();
    return hud.top <= canvas.top + canvas.height / 2 + 35;
  });
  assert.equal(overlap, false);

  for (const file of ['chapter1-offline.html','chapter2-offline.html','chapter3-offline.html','exhibit-hardware.html','vendor/hand_landmarker.task','vendor/vision_bundle.mjs','vendor/wasm/vision_wasm_internal.wasm','vendor/wasm/vision_wasm_nosimd_internal.wasm']) {
    const response = await context.request.head(new URL(file, base).href);
    assert.ok(response.ok(), file + ' returned ' + response.status());
  }
  assert.deepEqual(errors, []);
  await context.close();
}

async function verifyDeniedPermissions(browser) {
  const page = await browser.newPage();
  await page.addInitScript(() => {
    navigator.mediaDevices.getUserMedia = async () => {
      throw new DOMException('Permission denied during browser test', 'NotAllowedError');
    };
  });
  await page.goto(base.href);
  await page.locator('#camera-toggle').click();
  await page.waitForFunction(() => document.getElementById('signal-text').textContent.includes('未获得摄像头权限'));
  await page.locator('#mic-toggle').click();
  await page.waitForFunction(() => document.getElementById('audio-status').textContent.includes('无法开启麦克风'));
  assert.equal(await page.locator('#camera-toggle').isEnabled(), true);
  assert.equal(await page.locator('#mic-toggle').isEnabled(), true);
  assert.equal(await page.locator('#journey-clock').innerText(), '00:00');
  await page.close();
}

async function verifyTouchController(browser) {
  const page = await browser.newPage();
  await page.goto(base.href);
  await page.evaluate(async () => {
    const packets = [], choices = [];
    let input;
    const port = {
      readable: new ReadableStream({start(controller) { input = controller; }}),
      writable: new WritableStream({write(bytes) { packets.push(new TextDecoder().decode(bytes)); }}),
      async open() {},
      async close() {},
    };
    Object.defineProperty(navigator, 'serial', {value:{async requestPort() { return port; }}, configurable:true});
    const {installExhibitGame} = await import('./exhibit-game.js');
    const fixture = document.createElement('div');
    fixture.id = 'touch-controller-fixture';
    fixture.innerHTML = '<div></div><div><button data-touch-option></button><button data-touch-option></button></div>';
    document.body.append(fixture);
    const state = {packets, choices, blocked:false, send:line => input.enqueue(new TextEncoder().encode(line + '\n'))};
    state.controller = installExhibitGame(fixture.firstElementChild, fixture.lastElementChild, {
      isBlocked:() => state.blocked,
      respondToTouch:choice => { choices.push(choice); return true; },
    });
    state.controller.updateControls({labels:['林荫绕路','风带捷径'],selected:0},false);
    window.touchFixture = state;
  });
  const fixture = page.locator('#touch-controller-fixture');
  await fixture.getByRole('button', {name:/连接 Arduino/}).click();
  await page.waitForFunction(() => document.querySelector('#touch-controller-fixture #game-touch-status').textContent.includes('串口已打开'));
  await page.evaluate(() => window.touchFixture.send('EOL3 IN 0 3'));
  await fixture.getByRole('checkbox', {name:'启用实体灯光'}).check();
  await page.evaluate(() => {
    window.touchFixture.send('EOL3 IN 1 3');
    window.touchFixture.send('EOL3 IN 3 3');
  });
  await page.waitForFunction(() => window.touchFixture.packets.some(packet => / 12 4 0 0 3 12 -1\n$/.test(packet)));
  assert.deepEqual(await page.evaluate(() => window.touchFixture.choices), [0,1]);
  await page.evaluate(() => window.touchFixture.send('EOL3 IN 0 3'));
  await fixture.getByRole('button', {name:'B · 风带捷径'}).click();
  assert.deepEqual(await page.evaluate(() => window.touchFixture.choices), [0,1,1]);
  await page.evaluate(() => {
    const fixture = window.touchFixture;
    fixture.blocked = true;
    fixture.controller.update(performance.now());
  });
  await page.waitForFunction(() => / 0 0 0 0 0 0 -1\n$/.test(window.touchFixture.packets.at(-1)));
  assert.equal(await fixture.getByRole('checkbox', {name:'启用实体灯光'}).isChecked(), false);
  await fixture.getByRole('button', {name:'断开 Arduino'}).click();
  await page.close();
}

async function main() {
  const browser = await chromium.launch({channel:'msedge',headless:true,args:['--no-proxy-server']});
  try {
    await verifySite(browser);
    await verifyDeniedPermissions(browser);
    await verifyTouchController(browser);
    console.log('Passed: device wait and denial, input lock, checkpoint, projection, assets, and two-channel touches with mocked Web Serial.');
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
