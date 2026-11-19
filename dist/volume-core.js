/* ARTISTIC RATIONALE
   A quiet voice invites a slow journey; a fuller voice leaves a brighter trace.
   RMS loudness -> calibrated range -> attack/release smoothing -> world speed.
   This is relative microphone level (dBFS), not measured room sound pressure. */
import {clamp} from './core.js';

export const VOLUME_DEFAULTS = Object.freeze({quietDb:-48,loudDb:-18,maxSpeed:2.2,attack:.18,release:.65,trail:1.5});
export function volumeSettings(input={}) {
  const value=(key,lo,hi)=>clamp(Number.isFinite(Number(input[key]))?Number(input[key]):VOLUME_DEFAULTS[key],lo,hi);
  const quietDb=value('quietDb',-75,-15);
  return {quietDb,loudDb:Math.max(quietDb+6,value('loudDb',-60,-3)),maxSpeed:value('maxSpeed',.5,3),attack:value('attack',.05,1),release:value('release',.1,2),trail:value('trail',.5,3)};
}
export function rmsDb(samples) {
  if(!samples.length)return -100;
  let mean=0;for(const sample of samples){if(!Number.isFinite(sample))return -100;mean+=sample;}mean/=samples.length;
  let sum=0;for(const sample of samples)sum+=(sample-mean)**2;
  return clamp(20*Math.log10(Math.max(1e-5,Math.sqrt(sum/samples.length))),-100,0);
}
export function ambientCalibration(samples,settings=VOLUME_DEFAULTS) {
  const valid=samples.filter(Number.isFinite).sort((a,b)=>a-b);
  if(!valid.length)return volumeSettings(settings);
  const quietDb=clamp(valid[Math.floor((valid.length-1)*.9)]+6,-75,-15);
  return volumeSettings({...settings,quietDb,loudDb:Math.max(settings.loudDb,quietDb+18)});
}
export class VolumeEnvelope {
  constructor(settings){this.configure(settings);this.reset();}
  configure(settings){this.settings=volumeSettings(settings);}
  reset(){this.level=0;this.speed=0;}
  update(db,dt) {
    const s=this.settings,target=Number.isFinite(db)?clamp((db-s.quietDb)/(s.loudDb-s.quietDb),0,1):0;
    const shaped=target*target*(3-2*target),tau=shaped>this.level?s.attack:s.release;
    this.level+=(shaped-this.level)*(1-Math.exp(-clamp(dt,0,.1)/tau));
    if(this.level<.001)this.level=0;
    this.speed=this.level*s.maxSpeed;
    return {db:Number.isFinite(db)?db:-100,level:this.level,speed:this.speed};
  }
}
