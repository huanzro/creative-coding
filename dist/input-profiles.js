function profileKey(channel,track){
  if(!track)return null;
  const device=track.getSettings?.()?.deviceId||track.label||'default';
  return `echoes-${channel}-profile-v1/${device}`;
}

export function loadInputProfile(channel,track){
  const key=profileKey(channel,track);
  if(!key)return null;
  try{return JSON.parse(globalThis.localStorage.getItem(key));}
  catch{return null;}
}

export function saveInputProfile(channel,track,profile){
  const key=profileKey(channel,track);
  if(!key)return;
  try{globalThis.localStorage.setItem(key,JSON.stringify(profile));}
  catch{/* Device input still works when local storage is unavailable. */}
}
