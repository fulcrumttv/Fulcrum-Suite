import fs from 'node:fs';
import assert from 'node:assert/strict';
import {algorithm, src, buffer, car, Reader, body, subset} from './test_relative_lifecycle.mjs';

// Production control flow, mechanically translated by the existing harness.
// This is NOT a native C# build or an iRacing/SimHub telemetry recording.
const code=src('ClassPositionResolver.cs');
const oldCode=fs.readFileSync(new URL('./fixtures/ClassPositionResolver_v4155.cs.txt',import.meta.url),'utf8');
const previousCode=fs.readFileSync(new URL('./fixtures/ClassPositionResolver_v4156.cs.txt',import.meta.url),'utf8');
let checks=0,exhaustiveCases=0,sequenceFrames=0;
const eq=(a,b,m)=>{assert.deepEqual(a,b,m);checks++;};
function make(sizes=[3],source=code,slots=null){
 const b=buffer(),ids=Array(64).fill(null),groups=[];let n=0;
 for(let c=0;c<sizes.length;c++){
  const group=[];
  for(let p=0;p<sizes[c];p++,n++){
   const i=slots?.[n]??n;car(b,i,3,.2+n*.003);b[i].ClassId=100+c;
   ids[i]={IsValid:true,IsNonCompetitor:false,ClassId:100+c};group.push(i);
  }
  groups.push(group);
 }
 const r=algorithm(source,'ClassPositionResolver',['Reset','Update']);r.Reset();
 return {b,ids,groups,r,session:{Get:i=>ids[i]}};
}
function set(h,group,native,overall){for(let k=0;k<group.length;k++){h.b[group[k]].ClassPosition=native[k];h.b[group[k]].OverallPosition=overall[k];}}
const vector=(h,group)=>group.map(i=>h.b[i].ClassPosition);
const gains=(h,group)=>group.map(i=>h.b[i].PositionGainLoss);
function invariant(h){
 for(const group of h.groups){
  const active=group.filter(i=>h.ids[i]?.IsValid&&!h.ids[i]?.IsNonCompetitor);if(!active.length)continue;
  const ranks=vector(h,active),n=active.length;
  for(const i of active)eq(h.b[i].ClassSize,n,'class size includes every registered competitor');
  eq(ranks.every(x=>x===0)||ranks.slice().sort((a,b)=>a-b).every((x,i)=>x===i+1),true,'whole class is unique 1..N or wholly unavailable');
 }
}
const complete=(v,max)=>v.every(x=>x>=1&&x<=max)&&new Set(v).size===v.length;
const classFromOverall=v=>v.map(x=>1+v.filter(y=>y<x).length);
function partial(primary,fallback=null){
 const n=primary.length,out=Array(n).fill(0),seen=new Set();let known=0,missing=0;
 for(let i=0;i<n;i++){
  if(primary[i]>=1&&primary[i]<=n){if(seen.has(primary[i]))return null;seen.add(primary[i]);out[i]=primary[i];known++;}
  else missing++;
 }
 if(!known||!missing)return null;
 const unresolved=out.map((x,i)=>x?null:i).filter(x=>x!==null);
 if(missing>1){
  if(!fallback)return null;
  const values=unresolved.map(i=>fallback[i]);
  if(values.some(x=>x<1)||new Set(values).size!==values.length)return null;
  unresolved.sort((a,b)=>fallback[a]-fallback[b]);
 }
 const free=Array.from({length:n},(_,i)=>i+1).filter(x=>!seen.has(x));
 for(let k=0;k<free.length;k++)out[unresolved[k]]=free[k];
 return out;
}

// Negative fixture: 4.1.55 suppresses +/- solely because the session label
// is not Race. 4.1.56 establishes a reference and updates immediately.
for(const [source,available,label]of[[oldCode,false,'4.1.55 reproduction'],[code,true,'4.1.56 correction']]){
 const h=make([3],source),g=h.groups[0];set(h,g,[1,2,3],[1,2,3]);h.r.Update(h.b,h.session,false,4);
 eq(h.b[g[0]].PositionGainLossAvailable,available,label+' offline availability');
 set(h,g,[2,1,3],[2,1,3]);h.r.Update(h.b,h.session,false,4);
 eq(h.b[g[0]].PositionGainLossAvailable,available,label+' offline remains available');
 eq(gains(h,g),available?[-1,1,0]:[0,0,0],label+' offline change');
}

// Another delivered failure: one AI rank outside the class froze everyone.
for(const [source,expected]of[[oldCode,[0,0,0]],[code,[1,2,3]]]){
 const h=make([3],source),g=h.groups[0];set(h,g,[1,2,40],[0,0,0]);h.r.Update(h.b,h.session,true,4);
 eq(vector(h,g),expected,source===code?'sole unused rank repairs P40':'4.1.55 P40 freeze reproduced');
}

// Exhaustive small-state oracle. Complete telemetry wins, then complete
// overall order, then a coherent partial class, then the prior snapshot.
{
 const h=make(),g=h.groups[0],baseline=[2,3,1];
 for(const cached of [false,true])for(let bits=0;bits<15625;bits++){
  let q=bits;const v=[];for(let j=0;j<6;j++){v.push(q%5);q=Math.floor(q/5);}
  const native=v.slice(0,3),overall=v.slice(3);h.r.Reset();
  if(cached){set(h,g,baseline,[2,3,1]);h.r.Update(h.b,h.session,true,3);}
  set(h,g,native,overall);h.r.Update(h.b,h.session,true,4);
  const current=complete(native,3)?native:complete(overall,64)?classFromOverall(overall):partial(native,cached?baseline:null);
  const expected=current??(cached?baseline:[0,0,0]);
  eq(vector(h,g),expected,'exhaustive coherent source selection');invariant(h);
  const available=cached||current!==null;
  for(let k=0;k<3;k++){
   eq(h.b[g[k]].PositionGainLossAvailable,available,'gain has a captured reference exactly when a coherent source exists');
   if(available)eq(h.b[g[k]].PositionGainLoss,(cached?baseline[k]:expected[k])-expected[k],'gain uses stable reference');
  }
  exhaustiveCases++;
 }
}

// Every active session label, including offline AI, gets +/-; Lap remains
// unchanged to prove updates are not waiting for a finish-line crossing.
for(const type of ['Offline Testing','Practice','Qualifying','Lone Qualify','Warmup','Test Session'])for(const state of [1,2,3,4,5,6]){
 const h=make(),g=h.groups[0];set(h,g,[1,2,3],[1,2,3]);h.r.Update(h.b,h.session,false,state);
 eq(g.map(i=>h.b[i].PositionGainLossAvailable),[true,true,true],type+' state '+state+' baseline');
 const laps=g.map(i=>h.b[i].Lap);set(h,g,[2,1,3],[2,1,3]);h.r.Update(h.b,h.session,false,state);
 eq(gains(h,g),[-1,1,0],type+' state '+state+' immediate change');
 eq(g.map(i=>h.b[i].Lap),laps,type+' state '+state+' no lap dependency');
}

// Race behavior keeps a pre-green class grid. A late attach without qualifying
// now uses the first coherent live classification instead of hiding +/-.
{
 const h=make(),g=h.groups[0];
 const raw={SessionData:{QualifyResultsInfo:{Results:[0,1,2].map(i=>({CarIdx:i,Position:i}))}}};
 h.r.Update(h.b,h.session,true,3,raw);eq(vector(h,g),[1,2,3],'qualifying grid before green');
 set(h,g,[2,1,3],[2,1,3]);h.r.Update(h.b,h.session,true,4,raw);
 eq(gains(h,g),[-1,1,0],'early pass measured against pre-green grid');
 h.r.Reset();set(h,g,[3,1,2],[3,1,2]);h.r.Update(h.b,h.session,true,4,null);
 eq(g.map(i=>h.b[i].PositionGainLossAvailable),[true,true,true],'late race attach has usable baseline');
 eq(gains(h,g),[0,0,0],'late race baseline begins at zero');
 set(h,g,[2,1,3],[2,1,3]);h.r.Update(h.b,h.session,true,4,null);
 eq(gains(h,g),[1,0,-1],'late race changes update without a completed lap');
}

// 4.1.56 reproduced the reported mid-race restart defect: current-session
// QualifyPositions existed, but the resolver ignored it and captured the first
// live classification. 4.1.57 restores the original per-class start order.
{
 const raw={Telemetry:{SessionNum:2},SessionData:{SessionInfo:{CurrentSessionNum:2,Sessions:[
  {SessionNum:1,SessionType:'Lone Qualify',ResultsPositions:[]},
  {SessionNum:2,SessionType:'Race',QualifyPositions:[
   {CarIdx:0,ClassPosition:0},{CarIdx:1,ClassPosition:1},{CarIdx:2,ClassPosition:2},
   {CarIdx:3,ClassPosition:2},{CarIdx:4,ClassPosition:0},{CarIdx:5,ClassPosition:1}
  ]}
 ]}}};
 for(const [source,expected,label]of[
  [previousCode,[0,0,0],'4.1.56 mid-race restart reproduction'],
  [code,[-1,1,0],'4.1.57 historical grid recovery']
 ]){
  const h=make([3,3],source),a=h.groups[0],b=h.groups[1];
  set(h,a,[2,1,3],[2,1,3]);set(h,b,[1,3,2],[4,6,5]);h.r.Update(h.b,h.session,true,4,raw);
  eq(gains(h,a),expected,label+' class A');
  eq(gains(h,b),source===code?[2,-2,0]:[0,0,0],label+' class B');
  invariant(h);
 }

 const order=Array(64).fill(0);
 eq(Reader.ReadStartingClassOrder(raw,order),true,'current-session QualifyPositions found by SessionNum');
 eq(order.slice(0,6),[1,2,3,3,1,2],'zero-based ClassPosition converted per class');
 const globalRaw={Telemetry:{SessionNum:2},SessionData:{
  SessionInfo:{Sessions:[{SessionNum:2,SessionType:'Race'}]},
  QualifyResultsInfo:{Results:[0,1,2].map(i=>({CarIdx:i,ClassPosition:[1,2,0][i]}))}
 }};
 eq(Reader.ReadStartingClassOrder(globalRaw,order),true,'global qualifying ClassPosition fallback');
 eq(order.slice(0,3),[2,3,1],'global qualifying order remains per class');
 const priorRaw={Telemetry:{SessionNum:2},SessionData:{SessionInfo:{Sessions:[
  {SessionNum:1,SessionType:'Lone Qualify',ResultsPositions:[0,1,2].map(i=>({CarIdx:i,ClassPosition:[2,0,1][i]}))},
  {SessionNum:2,SessionType:'Race'}
 ]}}};
 eq(Reader.ReadStartingClassOrder(priorRaw,order),true,'preceding qualifying-session fallback');
 eq(order.slice(0,3),[3,1,2],'preceding results ClassPosition converted once');
 const currentInfoRaw={Telemetry:{SessionNum:2},CurrentSessionInfo:{SessionNum:2,
  QualifyPositions:[0,1,2].map(i=>({CarIdx:i,ClassPosition:i}))}};
 eq(Reader.ReadStartingClassOrder(currentInfoRaw,order),true,'matching CurrentSessionInfo fallback');
 eq(order.slice(0,3),[1,2,3],'CurrentSessionInfo class order');

 const delayed=make([3]),g=delayed.groups[0];set(delayed,g,[2,1,3],[2,1,3]);
 delayed.r.Update(delayed.b,delayed.session,true,4,null);
 eq(gains(delayed,g),[0,0,0],'temporary live baseline starts provisionally');
 delayed.r.Update(delayed.b,delayed.session,true,4,raw);
 eq(gains(delayed,g),[-1,1,0],'late metadata upgrades provisional baseline');
 set(delayed,g,[3,1,2],[3,1,2]);
 for(const flags of [0x4000,0x8000,0x200,0x4000]){
  const caution={Telemetry:{SessionNum:2,SessionFlags:flags}};
  delayed.r.Update(delayed.b,delayed.session,true,4,caution);
  eq(gains(delayed,g),[-2,1,1],'pits/tow/extended caution cannot downgrade or recapture grid');
 }

 const slow=make([3]),sg=slow.groups[0];set(slow,sg,[2,1,3],[2,1,3]);
 for(let frame=0;frame<130;frame++)slow.r.Update(slow.b,slow.session,true,4,null);
 for(let frame=0;frame<61;frame++)slow.r.Update(slow.b,slow.session,true,4,raw);
 eq(gains(slow,sg),[-1,1,0],'throttled long-delay polling still recovers historical grid');

 // An actual pre-green observation is more authoritative than metadata, for
 // example after a start-grid penalty. It remains fixed once green appears.
 const observed=make([3]),og=observed.groups[0];set(observed,og,[2,1,3],[2,1,3]);
 observed.r.Update(observed.b,observed.session,true,3,raw);
 set(observed,og,[1,2,3],[1,2,3]);observed.r.Update(observed.b,observed.session,true,4,raw);
 eq(gains(observed,og),[1,-1,0],'observed formation grid outranks conflicting history');

 raw.Telemetry.SessionNum=9;
 eq(Reader.ReadStartingClassOrder(raw,order),false,'wrong-session QualifyPositions rejected');
 eq(order.every(x=>x===0),true,'wrong-session read clears buffer');
 raw.Telemetry.SessionNum=2;
 raw.SessionData.SessionInfo.Sessions[1].QualifyPositions=[
  {CarIdx:0,ClassPosition:0},{CarIdx:0,ClassPosition:1},{CarIdx:1,ClassPosition:1}
 ];
 eq(Reader.ReadStartingClassOrder(raw,order),false,'duplicate CarIdx rejects historical snapshot');
 eq(order.every(x=>x===0),true,'duplicate historical snapshot clears buffer');
}

// Partial class telemetry: one absent rank is deterministic; multiple absent
// ranks need an existing coherent order; duplicate positives are never merged.
for(const [raw,expected]of[
 [[0,2,3],[1,2,3]],
 [[1,2,40],[1,2,3]],
 [[1,2,-1],[1,2,3]],
 [[1,2,999],[1,2,3]]
]){
 const h=make(),g=h.groups[0];set(h,g,raw,[0,0,0]);h.r.Update(h.b,h.session,false,4);
 eq(vector(h,g),expected,'one missing AI rank uses sole free slot');invariant(h);
}
{
 const h=make(),g=h.groups[0];set(h,g,[3,2,1],[3,2,1]);h.r.Update(h.b,h.session,false,4);
 set(h,g,[0,2,0],[0,0,0]);h.r.Update(h.b,h.session,false,4);
 eq(vector(h,g),[3,2,1],'two missing ranks ordered by coherent cache');
 set(h,g,[1,1,2],[0,0,0]);h.r.Update(h.b,h.session,false,4);
 eq(vector(h,g),[3,2,1],'duplicate positive ranks reject partial source');invariant(h);
 h.r.Reset();set(h,g,[0,2,0],[0,0,0]);h.r.Update(h.b,h.session,false,4);
 eq(vector(h,g),[0,0,0],'multiple missing ranks without evidence remain unavailable');
}

// Full reported 14/12/14 multiclasse, one invalid AI per class, pits, tow,
// garage and four extended caution segments. Class grids never recapture.
{
 const sizes=[14,12,14],n=sizes.reduce((a,b)=>a+b,0),slots=Array.from({length:n},(_,i)=>i);slots[n-1]=63;
 const h=make(sizes,code,slots);let offset=0;
 for(const group of h.groups){set(h,group,group.map((_,i)=>i+1),group.map((_,i)=>offset+i+1));offset+=group.length;}
 h.r.Update(h.b,h.session,true,3);
 const raw={Telemetry:{SessionState:4,SessionFlags:0,SessionTime:10,SessionNum:2}};
 for(let tick=0;tick<960;tick++){
  raw.Telemetry.SessionFlags=[0x4000,0x8000,0x4000,0x200][Math.floor(tick/240)];raw.Telemetry.SessionTime=10+tick;
  offset=0;
  for(const group of h.groups){
   const ranks=group.map((_,k)=>((k+Math.floor(tick/120))%group.length)+1),native=ranks.slice();
   native[tick%group.length]=tick%2?0:40+tick;const overall=ranks.map(x=>offset+x);offset+=group.length;
   set(h,group,native,overall);
   for(let k=0;k<group.length;k++){
    const i=group[k],mode=(tick+k)%4;car(h.b,i,2+Math.floor(tick/240),.2+k*.002,mode===1);
    if(mode>=2){h.b[i].IsValid=false;h.b[i].LapDistancePercent=-1;h.b[i].TrackSurface=-1;}
   }
  }
  h.r.Update(h.b,h.session,true,4,raw);invariant(h);
  for(const group of h.groups)for(let k=0;k<group.length;k++){
   const expected=((k+Math.floor(tick/120))%group.length)+1;
   eq(h.b[group[k]].ClassPosition,expected,'pits/tow/garage/caution preserve class rank');
   eq(h.b[group[k]].PositionGainLoss,(k+1)-expected,'extended caution keeps original grid');
  }
  sequenceFrames++;
 }
}

// Current SessionNum results work for all session types and never import a
// previous session. Session result ClassPosition is zero-based.
{
 const rows=[0,1,2].map(i=>({CarIdx:i,ClassPosition:[2,0,1][i],Position:[3,1,2][i]}));
 const raw={Telemetry:{SessionNum:2},SessionData:{SessionInfo:{Sessions:[
  {SessionNum:1,SessionType:'Race',ResultsPositions:[{CarIdx:0,ClassPosition:0,Position:1}]},
  {SessionNum:2,SessionType:'Practice',ResultsPositions:rows}
 ]}}};
 const cls=Array(64).fill(0),ov=Array(64).fill(0);
 eq(Reader.ReadSessionResults(raw,cls,ov),true,'current practice results accepted');
 eq(cls.slice(0,3),[3,1,2],'zero-based class results converted once');
 raw.SessionData.SessionInfo.Sessions[1].SessionType='Offline Testing';
 eq(Reader.ReadSessionResults(raw,cls,ov),true,'offline AI results accepted by identity, not label');
 const h=make(),g=h.groups[0];set(h,g,[0,0,0],[0,0,0]);h.r.Update(h.b,h.session,false,4,raw);
 eq(vector(h,g),[3,1,2],'non-race results drive complete class when telemetry ranks are absent');
 eq(g.map(i=>h.b[i].PositionGainLossAvailable),[true,true,true],'results-based offline reference is available');
 for(let i=0;i<3;i++){rows[i].ClassPosition=[1,2,0][i];rows[i].Position=[2,3,1][i];}
 set(h,g,[0,0,0],[0,0,0]);
 h.r.Update(h.b,h.session,false,4,raw);
 eq(vector(h,g),[2,3,1],'current non-race results continue updating');
 eq(gains(h,g),[1,-2,1],'results-based +/- retains first reference');
 raw.Telemetry.SessionNum=99;eq(Reader.ReadSessionResults(raw,cls,ov),false,'stale session results rejected');
 eq(cls.every(x=>x===0)&&ov.every(x=>x===0),true,'failed lookup clears result buffers');
 raw.Telemetry.SessionNum=2;raw.SessionData.SessionInfo.Sessions[1].ResultsPositions=[rows[0],rows[0],rows[1]];
 eq(Reader.ReadSessionResults(raw,cls,ov),false,'duplicate result rows reject whole snapshot');
}

// Exercise the actual module context/reset control flow. In particular, an
// Offline Testing label still drives ClassPositionResolver with isRace=false.
{
 const module=fs.readFileSync(new URL('../Fulcrum.Plugin/Modules/RelativeModule.cs',import.meta.url),'utf8');
 const h=make(),g=h.groups[0];
 const reader={...Reader,Integer:(v,f)=>v==null?f:Math.trunc(Number(v)),Number:(v,f)=>v==null?f:Number(v)};
 const run=new Function('h','RelativeSessionReader',`
  let latestRawData,latestSessionType='',latestPlayerCarIndex=0,previousRelativePlayer=-1;
  let hasRelativeSessionState=false,lastRelativeSessionState=-1,lastRelativeSessionNumber=-1,lastRelativeSessionTime=-1,lastRelativeSessionType='';
  let colors=false,resets=0;
  const participantBuffer=h.b,sessionDatabase=h.session,classPositions=h.r;
  const relativeCalculator={Reset(){resets++;},SetLapColorContext(v){colors=v;}};
  const stintTracker={Reset(){},SetContext(){}};
  const relativeTablePublisher={PublishContext(){}};
  ${subset(body(module,'UpdateRelativeRaceContext'))}
  return raw=>{latestRawData=raw;UpdateRelativeRaceContext();return {colors,resets};};
 `)(h,reader);
 const raw={Telemetry:{SessionState:4,SessionNum:2,SessionTime:10,SessionFlags:0},CurrentSessionInfo:{SessionType:'Offline Testing'}};
 set(h,g,[1,2,3],[1,2,3]);eq(run(raw).resets,1,'production module initializes offline context');
 eq(g.map(i=>h.b[i].PositionGainLossAvailable),[true,true,true],'production module exposes offline +/-');
 set(h,g,[2,1,3],[2,1,3]);raw.Telemetry.SessionTime=11;run(raw);
 eq(gains(h,g),[-1,1,0],'production module updates offline +/- immediately');
 raw.CurrentSessionInfo.SessionType='Race';raw.Telemetry.SessionTime=12;eq(run(raw).resets,1,'race/non-race switch does not reset colors or stints');
 eq(gains(h,g),[0,0,0],'race/non-race switch starts a new +/- reference');
 raw.Telemetry.SessionNum=3;raw.Telemetry.SessionTime=0;eq(run(raw).resets,2,'SessionNum change resets complete relative context');
}

console.log(JSON.stringify({
 status:'PASS',checks,exhaustiveCases,sequenceFrames,
 scope:'all-session class +/-; restart grid recovery; delayed metadata; offline AI; partial ranks; multiclass identity; pits/tow/garage and extended cautions; source-derived JS, not native C#'
},null,2));
