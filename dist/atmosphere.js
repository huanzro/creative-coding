const periods=[
  {name:'薄暮',warm:[236,165,99],sky:[35,26,46],strength:.12,mist:.8,night:.2,dawn:0},
  {name:'蓝调',warm:[122,183,223],sky:[18,33,62],strength:.16,mist:1,night:.6,dawn:0},
  {name:'月夜',warm:[159,188,247],sky:[8,14,37],strength:.23,mist:.7,night:1,dawn:0},
  {name:'破晓',warm:[255,207,149],sky:[70,40,52],strength:.10,mist:1.2,night:.1,dawn:1},
];
export function atmosphereAt(progress,preset='auto'){
  const fixed={dusk:0,blue:1,night:2,dawn:3}[preset];
  const p=fixed??Math.min(1,Math.max(0,progress))*3,i=Math.min(2,Math.floor(p)),f=p-i,t=f*f*(3-2*f),a=periods[i],b=periods[i+1];
  const mix=(x,y)=>x+(y-x)*t;
  return {name:periods[Math.round(p)].name,warm:a.warm.map((v,k)=>Math.round(mix(v,b.warm[k]))),sky:a.sky.map((v,k)=>Math.round(mix(v,b.sky[k]))),strength:mix(a.strength,b.strength),mist:mix(a.mist,b.mist),night:mix(a.night,b.night),dawn:mix(a.dawn,b.dawn)};
}

const images={};
const files={far:'chapter1-1.png',mid:'chapter1-0.png',front:'chapter1-2.png',geometry:'chapter2-0.png',stars:'chapter3-0.png'};
export async function loadAtmosphere(){
  const results=await Promise.allSettled(Object.entries(files).map(([key,file])=>new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>{images[key]=image;resolve();};image.onerror=()=>reject(file);image.src=new URL('./assets/'+file,import.meta.url).href;})));
  return results.filter(r=>r.status==='rejected').length===0;
}
const mod=(x,n)=>(x%n+n)%n;
function glow(ctx,x,y,r,color,alpha){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,`rgba(${color},${alpha})`);g.addColorStop(1,`rgba(${color},0)`);ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
function tiles(ctx,key,W,H,distance,height,y,speed,alpha=1){
  const image=images[key];if(!image)return;
  const width=height*image.width/image.height,offset=distance*H*speed;
  ctx.save();ctx.globalAlpha=alpha;
  for(let i=Math.floor(offset/width)-1;i<=Math.floor((offset+W)/width)+1;i++){
    const x=i*width-offset,flip=mod(i,2)===1;
    ctx.save();ctx.translate(x+(flip?width:0),y);if(flip)ctx.scale(-1,1);ctx.drawImage(image,0,0,width,height);ctx.restore();
  }ctx.restore();
}
export function drawAtmosphere(ctx,W,H,scene,sim,preset){
  const progress=Math.min(1,(sim.passed/sim.goal)*.8+sim.elapsed/150),phase=atmosphereAt(progress,preset),shift=(.5-sim.y)*H,t=sim.time;
  ctx.fillStyle='#07111e';ctx.fillRect(0,0,W,H);
  if(scene==='forest'){
    tiles(ctx,'far',W,H,sim.distance,H*1.5,-H*.25+shift*.15,.20);
    tiles(ctx,'mid',W,H,sim.distance,H*1.32,-H*.17+shift*.38,.48,.8);
    tiles(ctx,'front',W,H,sim.distance,H*1.25,-H*.08+shift*.7,.90,.95);
  }else tiles(ctx,scene,W,H,sim.distance,H*1.5,-H*.25+shift*.35,.35);
  // Render-time grading preserves the original reference plates on disk.
  ctx.fillStyle=`rgba(${phase.sky},${phase.strength})`;ctx.fillRect(0,0,W,H);
  ctx.save();ctx.globalCompositeOperation='screen';
  ctx.fillStyle=`rgba(${phase.warm},${.025*(1-phase.night)+phase.dawn*.12})`;ctx.fillRect(0,0,W,H);
  if(phase.dawn>0)glow(ctx,W*.8,H*.16+shift*.1,H*.65,phase.warm,.22*phase.dawn);
  for(let i=0;i<4;i++){
    const x=mod(i*W*.4-sim.distance*H*.25,W*1.6)-W*.25;
    ctx.save();ctx.translate(x,-H*.1);ctx.rotate(.25);const g=ctx.createLinearGradient(-40,0,80,0);g.addColorStop(0,`rgba(${phase.warm},0)`);g.addColorStop(.5,`rgba(${phase.warm},${.025+(1-phase.night)*.045})`);g.addColorStop(1,`rgba(${phase.warm},0)`);ctx.fillStyle=g;ctx.fillRect(-40,0,120,H*1.5);ctx.restore();
  }
  glow(ctx,W*.77,H*.3+shift*.12,H*.65,phase.warm,.055+(1-phase.night)*.04);
  for(let i=0;i<6;i++){const x=mod(i*W*.32-t*9,W*1.9)-W*.4;ctx.save();ctx.translate(x,H*.8+Math.sin(t*.13+i)*20+shift*.2);ctx.scale(1,.13);glow(ctx,0,0,W*.38,phase.warm,.055*phase.mist);ctx.restore();}
  for(let i=0;i<(scene==='stars'?70:35);i++){
    const x=mod(i*157.7-sim.distance*H*(.2+i%3*.15),W+80)-40,y=mod(i*91.3+Math.sin(t*.3+i)*12+shift*.2,H);
    const a=(.18+phase.night*.2)*(.6+.4*Math.sin(t*.7+i)**2);glow(ctx,x,y,5,phase.warm,a*.5);ctx.fillStyle=`rgba(${phase.warm},${a})`;ctx.fillRect(x,y,1.5,1.5);
  }
  // Sparse moonlit meteor streaks; visual atmosphere only, no hidden collisions.
  if(scene==='stars'&&phase.night>.45){for(let i=0;i<3;i++){const age=mod(t+i*6,19);if(age>1.4)continue;const x=W*(.7-i*.18)-age*140,y=H*.13+age*75;ctx.strokeStyle=`rgba(208,225,255,${(1-age/1.4)*.3})`;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+45,y-24);ctx.stroke();}}
  ctx.restore();
  const vignette=ctx.createRadialGradient(W*.5,H*.5,H*.1,W*.5,H*.5,Math.max(W,H)*.65);vignette.addColorStop(0,'#030a1000');vignette.addColorStop(1,'#030a1099');ctx.fillStyle=vignette;ctx.fillRect(0,0,W,H);
  return phase;
}
