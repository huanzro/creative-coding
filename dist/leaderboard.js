const KEY='echoes-of-light-rankings-v1';
export function rankRuns(runs){return [...runs].sort((a,b)=>b.score-a.score||b.passed-a.passed||a.elapsed-b.elapsed||a.id.localeCompare(b.id));}
export function boardRuns(runs,current){return rankRuns([...runs.filter(r=>r.level===current.level&&(r.mode||'gates')===current.mode&&r.id!==current.id),current]);}
export function loadRuns(storage){
  try{const data=JSON.parse(storage.getItem(KEY)||'[]');return Array.isArray(data)?data.filter(r=>r&&typeof r.id==='string'&&typeof r.name==='string'&&['forest','geometry','stars'].includes(r.level)&&Number.isFinite(r.score)&&r.score>=0&&Number.isFinite(r.elapsed)&&r.elapsed>=0&&Number.isInteger(r.passed)&&r.passed>=0).map(r=>({...r,mode:['gates','gather','resonance'].includes(r.mode)?r.mode:'gates'})).slice(0,90):[];}catch{return [];}
}
export function saveRun(storage,runs,run){
  if(run.passed<1)return {runs,saved:true};
  const next=[...runs.filter(r=>r.id!==run.id),{...run}].slice(-90);
  try{storage.setItem(KEY,JSON.stringify(next));return {runs:next,saved:true};}catch{return {runs:next,saved:false};}
}
