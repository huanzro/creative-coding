/* ARTISTIC RATIONALE
   Wind follows travel, while collected light and gesture responses illuminate
   the surrounding space. Preview and serial output share the same bounded values.
   Hardware stays unarmed until the operator explicitly enables it. */
import {clamp} from './core.js';
const unit=value=>Number.isFinite(value)?clamp(value,0,1):0;
export const OUTPUT_ZERO=Object.freeze({fan:0,light:0,rgb:[0,0,0]});
export function outputLevels({active=false,speed=0,glow=0,pulse=0,scene='forest',fanLimit=.65,lightLimit=.5}={}){
  if(!active)return {...OUTPUT_ZERO};
  const energy=unit(speed/3);
  return {fan:unit(fanLimit)*unit(energy*.8+unit(pulse)*.25),light:unit(lightLimit)*unit(.08+energy*.35+unit(glow)*.35+unit(pulse)*.6),rgb:scene==='forest'?[255,205,132]:scene==='geometry'?[150,181,255]:[205,225,255]};
}
export function encodeOutput(seq,levels=OUTPUT_ZERO){
  const byte=v=>Math.round(unit(v)*255),rgb=(levels.rgb||[0,0,0]).map(v=>Math.round(Number.isFinite(v)?clamp(v,0,255):0));
  return `EOL2 SET ${Math.max(0,Math.floor(seq))%65536} ${byte(levels.fan)} ${byte(levels.light)} ${rgb[0]||0} ${rgb[1]||0} ${rgb[2]||0} 800\n`;
}

// One write in flight; new state replaces pending state rather than building a queue.
// A receiver watchdog must zero outputs when no valid packet arrives for 800 ms.
export class SerialOutput {
  constructor(serial,onStatus=()=>{}){
    this.serial=serial;this.onStatus=onStatus;this.armed=false;this.busy=false;this.sequence=0;this.lastSent=-Infinity;this.port=null;this.writer=null;this.closing=false;
    this.onDisconnect=event=>{if(event.target===this.port||event.port===this.port){this.armed=false;void this.disconnect();}};
    serial?.addEventListener('disconnect',this.onDisconnect);
  }
  get connected(){return !!this.writer&&!this.closing;}
  async connect(){
    if(!this.serial)throw new Error('此浏览器不支持 Web Serial，请使用桌面 Edge / Chrome。');
    if(this.port||this.connecting)return;
    this.connecting=true;
    let port;
    try{port=await this.serial.requestPort();await port.open({baudRate:115200});const writer=port.writable.getWriter();this.port=port;this.writer=writer;this.armed=false;this.lastSent=-Infinity;this.onStatus('串口已连接 · 输出未启用');}
    catch(error){if(port&&port!==this.port)await port.close().catch(()=>{});throw error;}
    finally{this.connecting=false;}
  }
  setArmed(value){this.armed=!!value&&this.connected;if(!this.armed)this.send(OUTPUT_ZERO,performance.now(),true);}
  send(levels,now,force=false){
    if(!this.connected||this.busy||(!force&&now-this.lastSent<100))return;
    const writer=this.writer;this.busy=true;this.lastSent=now;
    const line=encodeOutput(this.sequence++,this.armed?levels:OUTPUT_ZERO);
    this.lastPacket=line.trim();
    this.pending=Promise.resolve().then(()=>writer.write(new TextEncoder().encode(line))).catch(error=>{
      this.armed=false;this.onStatus('串口写入失败：'+error.message);queueMicrotask(()=>void this.disconnect());
    }).finally(()=>{this.busy=false;});
  }
  async disconnect(){
    if(this.closing)return;this.closing=true;this.armed=false;
    const writer=this.writer,port=this.port;
    try{
      if(this.pending)await this.pending;
      if(writer)await writer.write(new TextEncoder().encode(encodeOutput(this.sequence++,OUTPUT_ZERO))).catch(()=>{});
    }finally{
      try{writer?.releaseLock();}catch{}
      this.writer=null;this.port=null;
      if(port)await port.close().catch(()=>{});
      this.closing=false;this.onStatus('串口已断开 · 仅预览');
    }
  }
}
