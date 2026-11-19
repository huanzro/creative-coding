import {rmsDb} from './volume-core.js';

// No connection to destination: microphone input is analysed, never played back.
export class MicrophoneInput {
  constructor(onEnded=()=>{}){this.onEnded=onEnded;this.generation=0;this.active=false;}
  async start(){
    const generation=++this.generation;
    let stream,context;
    try{
      if(!navigator.mediaDevices?.getUserMedia)throw new Error('请通过 localhost 或 HTTPS 打开，并允许麦克风。');
      stream=await navigator.mediaDevices.getUserMedia({audio:{autoGainControl:false,echoCancellation:false,noiseSuppression:false},video:false});
      if(generation!==this.generation){stream.getTracks().forEach(t=>t.stop());return false;}
      context=new AudioContext();await context.resume();
      if(generation!==this.generation){stream.getTracks().forEach(t=>t.stop());await context.close();return false;}
      this.stream=stream;this.context=context;this.source=context.createMediaStreamSource(stream);
      this.analyser=context.createAnalyser();this.analyser.fftSize=2048;
      this.samples=new Float32Array(this.analyser.fftSize);this.source.connect(this.analyser);this.active=true;
      for(const track of stream.getTracks())track.onended=()=>{if(generation===this.generation){this.stop();this.onEnded();}};
      return true;
    }catch(error){stream?.getTracks().forEach(t=>t.stop());if(context&&context.state!=='closed')await context.close().catch(()=>{});throw error;}
  }
  readDb(){if(!this.active||this.context.state!=='running')return -100;this.analyser.getFloatTimeDomainData(this.samples);return rmsDb(this.samples);}
  stop(){++this.generation;this.active=false;this.stream?.getTracks().forEach(t=>t.stop());this.source?.disconnect();this.context?.close().catch(()=>{});this.stream=null;this.context=null;}
}
