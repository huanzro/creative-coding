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

async function main() {
  const browser = await chromium.launch({channel:'msedge',headless:true,args:['--no-proxy-server']});
  try {
    await verifySite(browser);
    await verifyDeniedPermissions(browser);
    console.log('Passed: device wait and denial, input lock, checkpoint, projection, narrow layout, and all public asset routes.');
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
