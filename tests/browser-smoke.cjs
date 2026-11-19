const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert=require('node:assert/strict');
const path=require('node:path');
const fs=require('node:fs/promises');
let browser;
(async()=>{
  await fs.mkdir(path.resolve(__dirname,'../test-output'),{recursive:true});
  browser=await chromium.launch({channel:'msedge',headless:true,args:['--no-proxy-server']});
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];
  // Serve exact local dist bytes via test routing; no external network involved.
  await page.route('http://127.0.0.1:8876/**',async route=>{
    const url=new URL(route.request().url()),relative=decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname);
    const file=path.resolve(__dirname,'../dist','.'+relative),root=path.resolve(__dirname,'../dist')+path.sep;
    if(!file.startsWith(root))return route.fulfill({status:403,body:''});
    const types={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.css':'text/css','.png':'image/png','.wasm':'application/wasm'};
    try{await route.fulfill({status:200,body:await fs.readFile(file),contentType:types[path.extname(file)]||'application/octet-stream'});}catch{await route.fulfill({status:404,body:''});}
  });
  page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{
    window.lastOutput=null;window.addEventListener('echoes:outputs',e=>window.lastOutput=e.detail);
    navigator.mediaDevices.getUserMedia=async()=>{
      if(!window.syntheticAudio)throw new DOMException('Test rejection','NotAllowedError');
      const ctx=new AudioContext(),osc=ctx.createOscillator(),gain=ctx.createGain(),dest=ctx.createMediaStreamDestination();
      osc.frequency.value=220;gain.gain.value=.01;osc.connect(gain);gain.connect(dest);osc.start();await ctx.resume();
      window.audioFixture={ctx,osc,gain,dest};return dest.stream;
    };
  });
  await page.goto('http://127.0.0.1:8876/');
  await page.waitForSelector('#mic-toggle');
  await page.locator('#asset-status').waitFor({state:'hidden'});
  await page.locator('#mic-toggle').click();await page.waitForFunction(()=>document.querySelector('#audio-status').textContent.includes('无法开启'));
  assert.equal(await page.locator('#mic-toggle').isDisabled(),false);
  await page.locator('summary').filter({hasText:'声音与光迹调试'}).click();
  await page.locator('#audio-demo').check();
  const setRange=async(id,value)=>{await page.locator('#'+id).fill(String(value));await page.locator('#'+id).dispatchEvent('input');};
  await setRange('demo-db',-90);await page.waitForTimeout(3500);assert.ok(parseFloat(await page.locator('#volume-speed').innerText())<=.01);
  await setRange('demo-db',-32);await page.waitForTimeout(1300);const low=parseFloat(await page.locator('#volume-speed').innerText());assert.ok(low>.4&&low<1.7);
  await setRange('demo-db',-12);await page.waitForTimeout(1300);const high=parseFloat(await page.locator('#volume-speed').innerText());assert.ok(high>low&&high>2.1);
  assert.equal(await page.evaluate(()=>window.lastOutput.simulated),true);assert.equal(await page.evaluate(()=>window.lastOutput.armed),false);
  await page.locator('#obstacles').uncheck();
  await page.locator('#lift').click();await page.waitForTimeout(700);
  await page.screenshot({path:path.resolve(__dirname,'../test-output/volume-main.png')});
  const popupPromise=page.waitForEvent('popup');await page.getByRole('button',{name:'开启双屏投影 · 人物独立窗口'}).click();const popup=await popupPromise;
  popup.on('pageerror',e=>errors.push(e.message));await popup.waitForSelector('canvas');
  await page.waitForTimeout(800);await popup.screenshot({path:path.resolve(__dirname,'../test-output/volume-projection.png')});
  assert.ok(await popup.locator('canvas').evaluate(c=>c.width>0&&c.height>0));
  await popup.close();await page.waitForTimeout(100);
  await page.locator('#pause').click();await page.waitForTimeout(300);assert.equal(await page.evaluate(()=>window.lastOutput.preview.fan),0);
  await page.locator('#pause').click();await page.locator('#audio-demo').uncheck();
  await page.evaluate(()=>window.syntheticAudio=true);await page.locator('#mic-toggle').click();
  await page.waitForFunction(()=>document.querySelector('#audio-status').textContent.includes('音量控制已开启'));
  await page.waitForTimeout(1300);const quiet=parseFloat(await page.locator('#volume-db').innerText());
  await page.evaluate(()=>window.audioFixture.gain.gain.value=.3);await page.waitForTimeout(1300);
  const loud=parseFloat(await page.locator('#volume-db').innerText());assert.ok(loud-quiet>20);assert.ok(parseFloat(await page.locator('#volume-speed').innerText())>2);
  await page.evaluate(()=>window.audioFixture.gain.gain.value=0);await page.locator('#audio-calibrate').click();await page.waitForTimeout(2300);
  assert.ok((await page.locator('#audio-status').innerText()).includes('校准完成'));
  await page.locator('#mic-toggle').click();await page.waitForTimeout(150);assert.equal(await page.locator('#volume-speed').innerText(),'1.00×');
  await setRange('trail',2.1);await page.reload();await page.waitForSelector('#mic-toggle');assert.equal(await page.locator('#trail').inputValue(),'2.1');
  await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.resolve(__dirname,'../test-output/volume-mobile.png'),fullPage:true});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
  assert.deepEqual(errors,[]);console.log(JSON.stringify({passed:true,low,high,syntheticQuietDb:quiet,syntheticLoudDb:loud,pageErrors:errors,screenshots:['test-output/volume-main.png','test-output/volume-projection.png','test-output/volume-mobile.png']}));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();});
