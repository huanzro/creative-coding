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
  await page.locator('aside').evaluate(sidebar => { sidebar.scrollTop = sidebar.scrollHeight; });
  await page.locator('#journey-start-action').click();
  await page.waitForFunction(() => document.getElementById('pause-description').textContent.includes('未获得摄像头权限'));
  await page.locator('#mic-toggle').click();
  await page.waitForFunction(() => document.getElementById('audio-status').textContent.includes('无法开启麦克风'));
  assert.equal(await page.locator('#camera-toggle').isEnabled(), true);
  assert.equal(await page.locator('#mic-toggle').isEnabled(), true);
  assert.equal(await page.locator('#journey-clock').innerText(), '00:00');
  await page.close();
}

async function verifyStartupCard(browser) {
  const page = await browser.newPage();
  try {
    await page.addInitScript(() => {
      navigator.mediaDevices.getUserMedia = async ({video,audio}) => {
        if (video) {
          const canvas = document.createElement('canvas');
          canvas.width=320;canvas.height=240;
          const painter=canvas.getContext('2d');
          let frames=0;
          const stream=canvas.captureStream(20);
          setInterval(() => {painter.fillStyle=frames++ % 2 ? '#808080' : '#828282';painter.fillRect(0,0,320,240);},50);
          return stream;
        }
        if (audio) {
          const context=new AudioContext(),oscillator=context.createOscillator(),gain=context.createGain();
          const destination=context.createMediaStreamDestination();
          gain.gain.value=.001;
          oscillator.connect(gain).connect(destination);
          oscillator.start();
          await context.resume();
          window.startupGain=gain;
          return destination.stream;
        }
        throw new Error('Unexpected capture request');
      };
    });
    await page.goto(base.href);
    await page.locator('aside').evaluate(sidebar => { sidebar.scrollTop = sidebar.scrollHeight; });
    const action=page.locator('#journey-start-action');
    await action.click();
    await page.waitForFunction(() => document.getElementById('journey-start-action').textContent==='开启麦克风');
    await action.click();
    await page.waitForFunction(() => document.getElementById('pause-title').textContent==='保持安静');
    assert.match(await page.locator('#pause-description').innerText(), /秒/);
    await page.waitForFunction(() => document.getElementById('pause-title').textContent==='开始轻轻哼声');
    await page.evaluate(() => {window.startupGain.gain.value=.05;});
    await page.waitForFunction(() => document.getElementById('pause-title').textContent==='请把手放回画面');
    assert.equal(await action.isVisible(),false);
    assert.match(await page.locator('#audio-status').innerText(), /声音已就绪/);
    assert.equal(await page.locator('#journey-clock').innerText(), '00:00');
    assert.equal(await page.locator('[data-touch-option="0"]').isEnabled(),false);
  } finally {
    await page.close();
  }
}

async function verifyReactiveBackground(browser) {
  const page=await browser.newPage();
  try {
    await page.goto(base.href);
    const scenes=await page.evaluate(async () => {
      const {AtmosphereMotion,drawAtmosphere,loadAtmosphere}=await import('./atmosphere.js');
      if(!await loadAtmosphere())throw new Error('Background artwork did not load');
      const canvas=document.createElement('canvas');
      canvas.width=360;canvas.height=240;
      const context=canvas.getContext('2d',{willReadFrequently:true});
      const sim={voice:.4,holding:false,passed:0,goal:3,time:3,elapsed:0,y:.5,distance:0,lastPower:-Infinity};
      const frame=(scene,motion)=>{
        drawAtmosphere(context,360,240,scene,sim,'dusk',motion);
        return context.getImageData(0,0,360,240).data;
      };
      const mean=values=>{
        let total=0;
        for(let i=0;i<values.length;i+=4)total+=values[i]+values[i+1]+values[i+2];
        return total/(values.length/4*3);
      };
      const difference=(a,b)=>{
        let changed=0;
        for(let i=0;i<a.length;i+=4){
          if(Math.abs(a[i]-b[i])+Math.abs(a[i+1]-b[i+1])+Math.abs(a[i+2]-b[i+2])>=12)changed++;
        }
        return changed;
      };
      return ['forest','geometry','stars'].map(scene=>{
        const motion=new AtmosphereMotion();
        sim.holding=true;sim.lastPower=-Infinity;
        for(let i=0;i<90;i++)motion.update(1/60,sim,true);
        const closed=frame(scene,motion);
        sim.holding=false;
        for(let i=0;i<90;i++)motion.update(1/60,sim,true);
        const opened=frame(scene,motion);
        sim.lastPower=sim.time-.45;
        const released=frame(scene,motion);
        sim.lastPower=-Infinity;
        motion.drift+=.15;
        const moved=frame(scene,motion);
        return {scene,wake:mean(opened)-mean(closed),release:difference(opened,released),scroll:difference(opened,moved)};
      });
    });
    for(const state of scenes){
      assert.ok(state.wake>5, JSON.stringify(state));
      assert.ok(state.release>500, JSON.stringify(state));
      assert.ok(state.scroll>600, JSON.stringify(state));
    }
  } finally {
    await page.close();
  }
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
    await verifyStartupCard(browser);
    await verifyReactiveBackground(browser);
    await verifyTouchController(browser);
    console.log('Passed: device setup, three responsive backgrounds, checkpoints, projection, public assets, and mocked Web Serial.');
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
