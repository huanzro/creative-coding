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
const responseColors={forest:'255,200,112',geometry:'155,183,255',stars:'156,218,255'};

export class AtmosphereMotion {
  time=0;
  drift=0;
  wake=0;
  energy=0;

  update(dt,sim,active,scrollSpeed=1){
    this.time+=dt;
    const voice=active?sim.voice:0;
    const light=active?(sim.holding ? .24 : sim.hand==='unknown' ? .52 : .84):0;
    this.energy+=(voice-this.energy)*(1-Math.exp(-dt/.24));
    this.wake+=(light-this.wake)*(1-Math.exp(-dt/.5));
    if(active)this.drift+=dt*scrollSpeed*(.035+this.energy*.235)*(sim.holding ? .65 : 1);
  }
}

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
export function drawAtmosphere(ctx,W,H,scene,sim,preset,motion){
  const progress=Math.min(1,(sim.passed/sim.goal)*.8+sim.elapsed/150);
  const phase=atmosphereAt(progress,preset),shift=(.5-sim.y)*H,t=motion.time,distance=sim.distance+motion.drift;
  const {wake,energy}=motion,responseColor=responseColors[scene];
  ctx.fillStyle='#07111e';ctx.fillRect(0,0,W,H);
  if(scene==='forest'){
    tiles(ctx,'far',W,H,distance,H*1.5,-H*.25+shift*.15,.20);
    tiles(ctx,'mid',W,H,distance,H*1.32,-H*.17+shift*.38,.48,.8);
    tiles(ctx,'front',W,H,distance,H*1.25,-H*.08+shift*.7,.90,.95);
  }else tiles(ctx,scene,W,H,distance,H*1.5,-H*.25+shift*.35,.60);
  // Render-time grading preserves the original reference plates on disk.
  ctx.fillStyle=`rgba(${phase.sky},${phase.strength})`;ctx.fillRect(0,0,W,H);
  ctx.fillStyle=`rgba(3,10,24,${.24*(1-wake)})`;ctx.fillRect(0,0,W,H);
  ctx.save();ctx.globalCompositeOperation='screen';
  ctx.fillStyle=`rgba(${phase.warm},${.025*(1-phase.night)+phase.dawn*.12})`;ctx.fillRect(0,0,W,H);
  if(phase.dawn>0)glow(ctx,W*.8,H*.16+shift*.1,H*.65,phase.warm,.22*phase.dawn);
  glow(ctx,W*.24,H*.64+shift*.25,H*.70,responseColor,.04+wake*.30);
  glow(ctx,W*.77,H*.25+shift*.2,H*.67,responseColor,wake*.22+energy*.15);
  for(let i=0;i<4;i++){
    const x=mod(i*W*.4-distance*H*.25,W*1.6)-W*.25;
    ctx.save();ctx.translate(x,-H*.1);ctx.rotate(.25);const g=ctx.createLinearGradient(-40,0,80,0);g.addColorStop(0,`rgba(${responseColor},0)`);g.addColorStop(.5,`rgba(${responseColor},${.025+(1-phase.night)*.045+wake*.11})`);g.addColorStop(1,`rgba(${responseColor},0)`);ctx.fillStyle=g;ctx.fillRect(-40,0,120,H*1.5);ctx.restore();
  }
  glow(ctx,W*.77,H*.3+shift*.12,H*.65,phase.warm,.055+(1-phase.night)*.04);
  for(let i=0;i<6;i++){const x=mod(i*W*.32-t*9,W*1.9)-W*.4;ctx.save();ctx.translate(x,H*.8+Math.sin(t*.13+i)*20+shift*.2);ctx.scale(1,.13);glow(ctx,0,0,W*.38,phase.warm,.055*phase.mist);ctx.restore();}
  for(let i=0;i<(scene==='stars'?70:35);i++){
    const x=mod(i*157.7-distance*H*(.35+i%3*.28)-t*(5+i%4),W+80)-40;
    const y=mod(i*91.3+Math.sin(t*.55+i)*(13+energy*15)+shift*.2,H);
    const a=(.17+phase.night*.13+wake*.49)*(.48+.52*Math.sin(t*.8+i)**2);
    const size=1.3+wake*1.7+(i%3)*.45;
    if(i%3===0)glow(ctx,x,y,9+wake*9,responseColor,a*.4);
    ctx.fillStyle=`rgba(${responseColor},${a})`;ctx.fillRect(x,y,size,size);
  }
  // An accepted release illuminates the landscape beyond the character layer.
  const release=sim.time-sim.lastPower;
  if(release>=0&&release<2.2&&wake>.1){
    const progress=release/2.2,spread=Math.hypot(W,H)*(.14+.64*progress);
    const wave=ctx.createRadialGradient(W*.5,H*.5,spread*.72,W*.5,H*.5,spread*1.24);
    wave.addColorStop(0,`rgba(${responseColor},0)`);
    wave.addColorStop(.55,`rgba(${responseColor},${(1-progress)**1.4*wake*.31})`);
    wave.addColorStop(1,`rgba(${responseColor},0)`);
    ctx.fillStyle=wave;ctx.fillRect(0,0,W,H);
  }
  // Sparse moonlit meteor streaks; visual atmosphere only, no hidden collisions.
  if(scene==='stars'&&phase.night>.45){for(let i=0;i<3;i++){const age=mod(t+i*6,19);if(age>1.4)continue;const x=W*(.7-i*.18)-age*140,y=H*.13+age*75;ctx.strokeStyle=`rgba(208,225,255,${(1-age/1.4)*.3})`;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+45,y-24);ctx.stroke();}}
  ctx.restore();
  const vignette=ctx.createRadialGradient(W*.5,H*.5,H*.1,W*.5,H*.5,Math.max(W,H)*.65);vignette.addColorStop(0,'#030a1000');vignette.addColorStop(1,'#030a1099');ctx.fillStyle=vignette;ctx.fillRect(0,0,W,H);
  return phase;
}
