const GOLD='#f2d39b',BLUE='#a8d8f5',RED='#edaa98';
export function drawContinuity(ctx,W,H,s,echoSprite){
 const cx=W*.5,cy=H*.5;
 const text=(value,x,y,color='#dce5eb',size=13)=>{
  ctx.font=size+'px -apple-system, sans-serif';ctx.fillStyle=color;ctx.textAlign='center';
  const width=Math.min(W-24,ctx.measureText(value).width);
  const center=Math.max(width/2+12,Math.min(W-width/2-12,x));
  ctx.strokeStyle='#071321dd';ctx.lineWidth=3;ctx.lineJoin='round';
  ctx.strokeText(value,center,y,width);ctx.fillText(value,center,y,width);
 };
 const ring=(x,y,r,c,width=1)=>{ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.strokeStyle=c;ctx.lineWidth=width;ctx.stroke();};
 const path=(pts,c,width=1)=>{ctx.beginPath();pts.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.strokeStyle=c;ctx.lineWidth=width;ctx.stroke();};
 const bar=(x,y,w,p,c)=>{ctx.fillStyle='#08131dc9';ctx.fillRect(x,y,w,7);ctx.fillStyle=c;ctx.fillRect(x,y,w*Math.max(0,Math.min(1,p)),7);};
 const wave=(x,amp,sign,c,span=H*.23)=>{const pts=[];for(let i=0;i<=70;i++){const t=i/70;pts.push([x+Math.sin(t*Math.PI*4+s.time*2)*amp*H*.045*sign,cy-span/2+t*span]);}path(pts,c,2);};
 ctx.save();
 if(s.holding)ring(cx,cy,49,GOLD+'aa',2);else if(s.hand==='open')ring(cx,cy,58+s.voice*24,BLUE+'66');
 if(s.tutorial!==null){
  if(s.tutorial===0){text('张手：光展开',W*.3,H*.53,BLUE);text('握拳：光收拢',W*.72,H*.53,GOLD);ring(W*.3,H*.43,30,BLUE+'77');ring(W*.72,H*.43,15,GOLD+'99');}
  if(s.tutorial===1){bar(W*.65,H*.49,W*.22,s.voice,GOLD);text('你的舒适音量',W*.76,H*.45);text('轻声',W*.67,H*.56,BLUE);text('舒适发声',W*.84,H*.56,GOLD);}
  if(s.tutorial===2){for(let i=0;i<2;i++){const y=H*(.35+i*.3);path([[cx,cy],[W*.69,y],[W*.92,y]],s.route===i?GOLD:'#b6c4ce44',s.route===i?3:1);text(i?'风带捷径':'林荫绕路',W*.79,y-18,s.route===i?GOLD:'#b6c4ce');}}
 }else if(s.levelIndex===0){
  for(let i=0;i<s.flock;i++){const a=i*2.4+s.time*.5,r=s.holding?40:70;ring(cx-Math.abs(Math.cos(a))*r-24,cy+Math.sin(a)*r*.48,3.5,GOLD,3);}
  if(s.phase==='gather'){
   for(let i=0;i<5;i++)ring(W*(.72+i*.045),H*(.43+Math.sin(i+s.time)*.06),3,BLUE+'aa',2);
   text(s.voice>.7?'声音太响，萤火在退开':s.quiet&&s.hand==='open'?'它们正在靠近':'张手，轻轻哼声',W*.77,H*.62,s.voice>.7?RED:GOLD);
   bar(W*.67,H*.66,W*.2,s.progress,GOLD);
  }
  if(s.phase==='fork')for(let i=0;i<2;i++){
   const y=H*(.32+i*.32);path([[cx,cy],[W*.7,y],[W*.96,y]],s.route===i?GOLD:'#b9d4db44',s.route===i?3:1);
   text(i?'风带捷径 · 6 秒 · 成功多一束光':'林荫绕路 · 10 秒 · 张手轻声',W*.77,y-18,s.route===i?GOLD:'#c5ced5');
  }
  if(s.phase==='travel'){
   if(s.route)for(let i=0;i<=s.round;i++){
    const at=(i+1)/(s.round+2),x=cx+(at-s.progress)*W*.7;if(x<cx-40)continue;
    for(let j=0;j<5;j++)path([[x-15,H*(.32+j*.08)],[x+18,H*(.29+j*.08)],[x-8,H*(.36+j*.08)]],at-s.progress<.18?GOLD:'#bfdae277',2);
    text(at-s.progress<.18?'风要到了 · 握拳':'风带',x,H*.27,GOLD);
   }else{text(s.hand==='open'&&s.quiet?'萤火跟得很稳':'队伍开始散开 · 张手轻声',W*.77,H*.38,s.hand==='open'&&s.quiet?GOLD:RED);bar(W*.67,H*.44,W*.2,1-s.lossTime/2,GOLD);}
   bar(W*.67,H*.7,W*.22,s.progress,GOLD);
  }
  if(s.phase==='nest'){
   const x=W*.75,y=H*.48;for(let i=0;i<6;i++)ring(x+Math.cos(i*Math.PI/3)*19,y+Math.sin(i*Math.PI/3)*19,16,GOLD);text('张手轻声，让它们回家',x,y+73);bar(x-60,y+88,120,s.progress/1.2,GOLD);
  }
 }else if(s.levelIndex===1){
  const doorX=s.phase==='cross'?W*(.92-s.phaseTime*.18):W*.82;
  const gap=s.phase==='cross'?H*.32:H*.045;
  for(const [a,b]of [[H*.14,cy-gap],[cy+gap,H*.82]]){ctx.fillStyle='#324967aa';ctx.fillRect(doorX-10,a,20,b-a);path([[doorX-16,a],[doorX+16,a]],BLUE,2);path([[doorX-16,b],[doorX+16,b]],BLUE,2);}
  const recording=s.phase==='record',playing=s.phase==='replay',slots=s.round+2;
  for(let i=0;i<slots;i++){
   const x=W*(.64+i*.075),y=H*.29,active=i===s.slot;
   ring(x,y,active?19:13,active?GOLD:BLUE+'66',active?2:1);
   text(s.recording[i]?s.recording[i]==='closed'?'握':'张':String(i+1),x,y+5);
  }
  if(recording){text('世界在记住你的动作',W*.73,H*.64,GOLD);bar(W*.63,H*.68,W*.22,s.slotTime/s.slotLength,GOLD);}
  if(playing){
   const recorded=s.recording[s.slot],good=s.hand!==recorded&&s.balanced;
   ctx.save();ctx.globalAlpha=.38;echoSprite?.(W*.31,cy,recorded);ctx.restore();
   text('回声：'+(recorded==='closed'?'握拳':'张手'),W*.31,cy-66,BLUE);
   text('你：'+(recorded==='closed'?'张手':'握拳'),W*.5,cy-66,GOLD);
   path([[W*.31,cy+64],[doorX,cy+64]],good?GOLD:RED+'88',good?3:1);
   text(good?'双通道正在接通':s.hand===recorded?'两边动作相同，换成相反动作':'把声音维持在中间亮区',W*.7,H*.67,good?GOLD:RED);
   bar(W*.64,H*.71,W*.2,s.slotGood/s.replayRequired,good?GOLD:RED);
  }
  if(['record-ready','replay-ready','failed'].includes(s.phase))text('触摸 A 或 B · '+s.touchControls.labels[0],W*.72,H*.65,GOLD);
 }else{
  if(s.phase==='wave'){
   const w=s.objects[0];wave(w.x*W,w.amp,1,w.kind==='storm'?RED:BLUE);
   text(w.kind==='storm'?'风暴 · 需要抵消':'星灯 · 需要增强',w.x*W,H*.28,w.kind==='storm'?RED:BLUE);
   if(s.pending)wave(s.pending.x*W,s.pending.amp,s.pending.sign,GOLD);
   if(s.holding){
    ctx.setLineDash([5,6]);path([[cx,cy],[w.x*W,cy]],GOLD+'77');ctx.setLineDash([]);
    const width=W*(W<560?.48:.28),x=W-width-22,y=H*.70;
    const toX=value=>x+(value+.8)/2.6*width;
    const target=s.waveTarget,tolerance=s.waveTolerance;
    ctx.fillStyle='#071321dd';ctx.fillRect(x,y,width,14);
    ctx.fillStyle='#91bdaa77';ctx.fillRect(toX(target-tolerance),y,toX(target+tolerance)-toX(target-tolerance),14);
    const marker=toX(s.resultant);
    path([[marker,y-7],[marker,y+21]],s.waveReady?GOLD:RED,3);
    text((s.waveReady?'张手释放':w.kind==='storm'?'目标：平静':'目标：点亮')+' · '+s.resultant.toFixed(2),x+width/2,y-16,s.waveReady?GOLD:'#e0e6ed');
    text(s.color?'反相 −':'同相 +',cx-20,cy+101,GOLD);
   }
  }
  if(['resolved','wave-retry'].includes(s.phase)&&s.lastResult){
   const {kind,result,good}=s.lastResult;
   const color=good?GOLD:RED;
   if(good&&kind==='beacon'){
    const shine=ctx.createRadialGradient(W*.76,cy,0,W*.76,cy,62);
    shine.addColorStop(0,'#f5dfa8dd');shine.addColorStop(1,'#f5dfa800');
    ctx.fillStyle=shine;ctx.fillRect(W*.76-62,cy-62,124,124);
   }else{
    wave(W*.76,Math.min(1,Math.abs(result)),Math.sign(result),color);
   }
   ring(W*.76,cy,38+s.phaseTime*13,color+'aa',good?1:2);
   text((good?(kind==='storm'?'风暴平息':'星灯点亮'):'再试同一道波')+' · '+result.toFixed(2),W*.76,H*.65,color);
  }
  if(['compose','answer'].includes(s.phase)){
   for(let i=0;i<3;i++){
    const p=s.userPhrase[i],x=W*(.65+i*.10);if(p)wave(x,p.amp,p.sign,GOLD,H*.15);else ring(x,cy,10,'#b4cedd55');
    if(p&&s.phase==='answer'&&Math.floor(s.phaseTime/2)>=i)wave(x+10,p.amp,-p.sign,BLUE,H*.15);
   }
   text(s.phase==='compose'?'发出你的三道波，世界会接着说':'金色是你的声音，蓝色是世界补上的回应',W*.73,H*.68,GOLD);
  }
 }
 if(s.levelIndex!==2||s.tutorial!==null){
  const lo=s.tutorial===1&&s.holding?.55:s.levelIndex===1?.18:.12,hi=s.tutorial===1&&s.holding?.9:s.levelIndex===1?.8:.45;
  const x=cx-90,y=cy+104,width=180;ctx.fillStyle='#071321bb';ctx.fillRect(x,y,width,7);ctx.fillStyle='#8ebb9e88';ctx.fillRect(x+lo*width,y,(hi-lo)*width,7);path([[x+s.voice*width,y-4],[x+s.voice*width,y+12]],GOLD,2);text('声音 · 标记进入亮区',cx,y+30,'#c6d5dd',12);
 }
 text(s.hand==='unknown'?(s.started?'旅程继续':'等待真实手势'):s.holding?'握拳 · 收束':'张手 · 展开',cx,cy+77,s.holding?GOLD:BLUE);
 ctx.restore();
}
