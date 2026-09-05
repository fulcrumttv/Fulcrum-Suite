import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';

const here=path.dirname(fileURLToPath(import.meta.url));
const root=path.dirname(here);
const kit=root;
const core=path.join(kit,'Fulcrum.Core/Relative');
let checks=0;
function eq(actual,expected,label){if(typeof actual==='number'&&actual===0)actual=0;assert.deepEqual(actual,expected,label);checks++;}
function ok(value,label){assert.ok(value,label);checks++;}
function src(name){return fs.readFileSync(path.join(core,name),'utf8');}
function body(s,name){const start=s.search(new RegExp('(?:public|private) (?:static )?[^\\n]+ '+name+'\\('));assert.ok(start>=0,name);let from=s.indexOf('{',start),depth=1,i=from+1;for(;depth;i++){if(s[i]==='{')depth++;if(s[i]==='}')depth--;}return s.slice(start,i);}

// Narrow mechanical C# subset: executed control flow is read from the shipped
// source. Reflection/BCL access is shimmed here; actual .NET reflection and the
// complete module->publisher route are tested by RelativeIntegrationTests.cs.
function subset(s){return s.replace(/\/\*[\s\S]*?\*\//g,'').replace(/\/\/[^\n]*/g,'')
 .replace(/private const int (\w+) = ParticipantBuffer.DefaultCapacity;/g,'const $1=64;')
 .replace(/private readonly RelativeLapTracker (\w+) = new RelativeLapTracker\(\);/g,'const $1=lapper();')
 .replace(/private readonly (bool|int|float|double)\[\] (\w+) = new \w+\[(\w+)\];/g,(_,t,n,z)=>`const ${n}=Array(${z}).fill(${t==='bool'?'false':'0'});`)
 .replace(/private (bool|int|float|double) (\w+)(?: = ([^;]+))?;/g,(_,t,n,v)=>`let ${n}=${v??(t==='bool'?'false':'0')};`)
 .replace(/(?:public|private) (?:static )?(?:void|int|double|float|bool|object|string) (\w+)\(([^)]*)\)/g,(_,n,a)=>`function ${n}(${a.replace(/\b(?:bool|int\[\]|int|double|float|object|string|ParticipantBuffer|ParticipantSnapshot|SessionDatabase)\s+/g,'')})`)
 .replace(/foreach\s*\(object (\w+) in (\w+)\)/g,'for(let $1 of $2)')
 .replace(/\b(?:ParticipantSnapshot|DriverIdentity|int\[\]|int|double|float|bool|string|object|IEnumerable)\s+(\w+)\s*=/g,'let $1=')
 .replace(/ as IEnumerable/g,'')
 .replace(/\(int\)/g,'').replace(/(\d(?:\.\d+)?)f\b/g,'Math.fround($1)').replace(/\.Length\b/g,'.length')
 .replace(/Math\.(Abs|Round|Max|Min|Floor|Ceiling)/g,(_,n)=>'Math.'+({Abs:'abs',Round:'round',Max:'max',Min:'min',Floor:'floor',Ceiling:'ceil'}[n]))
 .replace(/double\.IsNaN\((\w+)\)/g,'Number.isNaN($1)')
 .replace(/double\.IsInfinity\((\w+)\)/g,'(Math.abs($1)===Infinity)')
 .replace(/Array.Clear\((\w+), 0, [^)]+\)/g,'$1.fill(0)')
 .replace(/\.Replace\(/g,'.replaceAll(').replace(/\.ToLowerInvariant\(\)/g,'.toLowerCase()').replace(/\.Trim\(\)/g,'.trim()')
 .replace(/(\w+)\.IndexOf\("(\w+)", StringComparison.OrdinalIgnoreCase\)/g,'$1.toLowerCase().indexOf("$2")')
 .replace(/string.Empty/g,"''").replace(/Convert.ToString\(value, CultureInfo.InvariantCulture\)/g,'String(value)');}
const Member=(obj,key)=>obj?.[key]??null;
const Integer=(v,f)=>v===null||v===undefined||!Number.isFinite(Number(v))?f:Math.trunc(Number(v));
const readerCode=['Telemetry','State','SessionType','ReadQualifyingOrder','ReadStartingClassOrder','ReadOrder','ReadSessionResults','ReadRaceResults','ReadResultRows','ReadClassOrder','SessionData','Text'].map(n=>subset(body(src('RelativeSessionReader.cs'),n))).join('\n');
const Reader=new Function('Member','Integer',readerCode+';return {Telemetry,State,SessionType,ReadQualifyingOrder,ReadStartingClassOrder,ReadSessionResults,ReadRaceResults};')(Member,Integer);
function algorithm(s,name,methods){s=s.slice(s.indexOf('public sealed class '+name));s=s.slice(s.indexOf('{')+1,s.lastIndexOf('}'));s=s.slice(0,s.lastIndexOf('}'));return new Function('lapper','RelativeSessionReader',subset(s)+';return {'+methods.join(',')+'};')(lapper,Reader);}
function lapper(){const l=algorithm(src('RelativeLapTracker.cs'),'RelativeLapTracker',['Reset','SetContext','Update','CompletedLaps','ContinuousPosition','LapDifference']);l.Reset();return l;}
function tracker(){const t=algorithm(src('StintTracker.cs'),'StintTracker',['Reset','SetContext','Update','GetStintLap','IsOutLap','IsTowing']);t.Reset();return t;}
function buffer(){const a=Array.from({length:64},(_,i)=>({CarIndex:i,IsValid:false,IsPlayer:i===0,Lap:-1,LapCompleted:-1,LapDistancePercent:-1,TrackSurface:-1,ClassId:-1,ClassPosition:0,OverallPosition:0,IsOnPitRoad:false}));a.Capacity=64;return a;}
function car(b,i,lap,pct,pit=false){Object.assign(b[i],{IsValid:true,Lap:lap,LapCompleted:lap-1,LapDistancePercent:Math.fround(pct),TrackSurface:pit?1:3,IsOnPitRoad:pit});}

// Negative fixtures from the actually delivered 4.1.52 / 6.6.96 package.
// Reproduce the defects before asserting their replacement behavior.
{
 const old=algorithm(fs.readFileSync(path.join(here,'fixtures/StintTracker_v4152.cs.txt'),'utf8'),'StintTracker',['Reset','Update','GetStintLap','IsOutLap']);old.Reset();
 const b=buffer();for(let i=0;i<3;i++)car(b,i,8,.50+i*.003);old.Update(b);old.Update(b);
 for(let i=0;i<3;i++)car(b,i,0,.90+i*.003,i===0);old.Update(b);
 for(let i=0;i<3;i++)car(b,i,0,.91+i*.003);old.Update(b);
 eq(old.IsOutLap(0),true,'old formation reproduced as false OUT');
 for(let i=0;i<3;i++)car(b,i,1,.10+i*.003);old.Update(b);
 eq(old.GetStintLap(1),0,'old practice baseline leaves rival stint empty at green');
 const fixture=JSON.parse(fs.readFileSync(path.join(here,'fixtures/dashboard_v6696.json'),'utf8'));
 const p={'FulcrumPlugin.Fulcrum.Relative.Table.Row05.StintLap':old.GetStintLap(0)};
 eq(new Function('$prop',fixture.status)(k=>p[k]??null),'L1','old player-only reset and dashboard correction reproduce L1');
 eq(fixture.position.BackgroundColor,'#00000000','old fixed fifth slot loses its POS background for Alex');
}

// Real raw-frame shape, not an artificial top-level SessionState property.
for(const state of [3,4,'3','4','ParadeLaps','Racing','irsdk_StateRacing']){
 const raw={Telemetry:{SessionState:state,SessionNum:2},SessionState:1,
  SessionData:{SessionInfo:{Sessions:[{SessionNum:1,SessionType:'Lone Qualify'},{SessionNum:2,SessionType:'Race'}]}}};
 const expected=String(state).includes('3')||state==='ParadeLaps'?3:4;
 eq(Reader.State(raw),expected,'nested state overrides frame root');eq(Reader.SessionType(raw,''),'Race','SessionData race fallback');
 raw.CurrentSessionInfo={SessionType:'Race'};eq(Reader.SessionType(raw,''),'Race','dictionary CurrentSessionInfo');
}

// Qualifying uses zero-based Position; ResultsPositions uses one-based Position.
for(const sizes of [[3,3],[14,12,14],[1,7,23]])for(const format of ['QualifyResultsInfo','ResultsPositions']){
 const b=buffer(),ids=Array(64).fill(null),results=[];
 let idx=0;for(let c=0;c<sizes.length;c++)for(let rank=1;rank<=sizes[c];rank++,idx++){
  car(b,idx,0,.90+idx*.001);b[idx].ClassId=100+c;
  ids[idx]={IsValid:true,IsNonCompetitor:false,ClassId:100+c};results.push({CarIdx:idx,Position:idx+(format==='QualifyResultsInfo'?0:1)});
 }
 const raw={Telemetry:{SessionNum:2},SessionData:format==='QualifyResultsInfo'?{QualifyResultsInfo:{Results:results}}:{SessionInfo:{Sessions:[{SessionNum:1,SessionType:'Lone Qualify',ResultsPositions:results}]}}};
 const cr=algorithm(src('ClassPositionResolver.cs'),'ClassPositionResolver',['Reset','Update']);cr.Reset();
 const session={Get:i=>ids[i]};cr.Update(b,session,true,3,raw);
 for(let i=0;i<idx;i++)eq(b[i].ClassPosition,i-results.findIndex(x=>ids[x.CarIdx].ClassId===b[i].ClassId)+1,'grid POS exists even when raw positions are zero');
 for(let i=0;i<idx;i++){b[i].OverallPosition=i+1;b[i].ClassPosition=i-results.findIndex(x=>ids[x.CarIdx].ClassId===b[i].ClassId)+1;}
 cr.Update(b,session,true,4,raw);
 for(let i=0;i<idx;i++){eq(b[i].PositionGainLossAvailable,true,'grid was not permanently locked empty');eq(b[i].PositionGainLoss,0,'same starting position is zero');}
 const start=sizes[0];b[start].ClassPosition=2;b[start+1].ClassPosition=1;b[start].OverallPosition=start+2;b[start+1].OverallPosition=start+1;
 cr.Update(b,session,true,4,raw);eq(b[start].PositionGainLoss,-1,'class loss restored');eq(b[start+1].PositionGainLoss,1,'class gain restored');
}

// No qualifying section: capture native class grid on the observed green edge.
{
 const b=buffer(), cr=algorithm(src('ClassPositionResolver.cs'),'ClassPositionResolver',['Reset','Update']);cr.Reset();
 const ids=Array(64).fill(null);for(let i=0;i<3;i++){car(b,i,0,.9+i*.003);ids[i]={IsValid:true,IsNonCompetitor:false,ClassId:100};}
 const session={Get:i=>ids[i]};cr.Update(b,session,true,3,null);
 for(let i=0;i<3;i++){b[i].ClassPosition=i+1;b[i].OverallPosition=i+1;}
 cr.Update(b,session,true,4,null);eq(b[0].PositionGainLossAvailable,true,'native grid arriving at green is captured');
 b[0].ClassPosition=2;b[1].ClassPosition=1;cr.Update(b,session,true,4,null);eq(b[0].PositionGainLoss,-1,'native grid is not recaptured after start');
 cr.Reset();cr.Update(b,session,true,4,null);
 eq(b[0].PositionGainLossAvailable,true,'late attach records the first coherent classification as its reference');
 eq(b[0].PositionGainLoss,0,'late-attach reference begins at zero');
}

// Formation after a practice session, with random pit flags during grid placement.
for(const startState of [1,3]){
 const b=buffer(),t=tracker();for(let i=0;i<6;i++)car(b,i,10+i,.5+i*.002);
 t.SetContext(false,4,100);t.Update(b);t.Update(b);
 t.Reset(); // production module resets all race-dependent trackers on SessionNum change
 for(let i=0;i<6;i++)car(b,i,0,.90+i*.003,i%2===0);
 t.SetContext(true,startState,0);t.Update(b);
 for(let i=0;i<6;i++)car(b,i,0,.91+i*.003);
 t.SetContext(true,startState,1);t.Update(b);
 for(let i=0;i<6;i++){eq(t.IsOutLap(i),false,'grid is not a pit exit');eq(t.GetStintLap(i),0,'formation is not counted');}
 for(let tick=0;tick<1200;tick++){
  const p=-.10+tick*.001;
  for(let i=0;i<6;i++){const v=p+i*.003;car(b,i,Math.floor(v)+1,v-Math.floor(v));b[i].LapCompleted=-1;}
  t.SetContext(true,4,2+tick*.05);t.Update(b);
  for(let i=0;i<6;i++){eq(t.IsOutLap(i),false,'race start is not OUT');eq(t.GetStintLap(i),p+i*.003<1?1:2,'all cars use L1 then L2, no dashboard offset');}
 }
}

// Real pit exit, not grid: OUT -> L1 -> L2 for BOTH player and other cars.
{
 const b=buffer(),t=tracker();for(let i=0;i<2;i++)car(b,i,4,.90+i*.003);
 t.SetContext(true,4,300);t.Update(b);
 for(let i=0;i<2;i++)car(b,i,4,.92+i*.003,true);t.SetContext(true,4,301);t.Update(b);
 for(let i=0;i<2;i++)car(b,i,4,.94+i*.003);t.SetContext(true,4,302);t.Update(b);
 for(let i=0;i<2;i++)eq(t.IsOutLap(i),true,'real pit exit remains OUT');
 for(let i=0;i<2;i++)car(b,i,4,.995+i*.002);t.SetContext(true,4,303);t.Update(b);
 for(let i=0;i<2;i++)car(b,i,5,.005+i*.002);t.SetContext(true,4,304);t.Update(b);
 for(let i=0;i<2;i++){eq(t.IsOutLap(i),false,'OUT clears after line');eq(t.GetStintLap(i),1,'timed stint begins at L1');}
 for(let tick=1;tick<=101;tick++){for(let i=0;i<2;i++){const p=.005+tick*.01+i*.002;car(b,i,p>=1?6:5,p%1);}t.SetContext(true,4,304+tick*.1);t.Update(b);}
 for(let i=0;i<2;i++)eq(t.GetStintLap(i),2,'next completed stint lap gives L2');
}

// Lap colors are separate from stint status, but now get real enabled context.
{
 const raw={Telemetry:{SessionState:4},CurrentSessionInfo:{SessionType:'Race'}};
 const b=buffer(),l=lapper();car(b,0,4,.50);car(b,1,5,.49);car(b,2,3,.51);
 l.SetContext(Reader.State(raw)>=4&&Reader.SessionType(raw,'')==='Race',300);l.Update(b);
 eq(l.LapDifference(b[0],b[1]),1,'actual nested racing state enables red lapper');
 eq(l.LapDifference(b[0],b[2]),-1,'actual nested racing state enables blue backmarker');
}

// Keep the previous finish-line regression checks, now with context resolved from telemetry.
for(const early of [false,true]){
 const b=buffer(),l=lapper();
 for(let tick=0;tick<450;tick++){
  for(let i=0;i<3;i++){const v=2.80+tick*.001+(i-1)*.018;car(b,i,Math.floor(v+(early?.003:-.003))+1,v-Math.floor(v));}
  l.SetContext(true,tick*.05);l.Update(b);
  eq(l.LapDifference(b[1],b[0]),0,'same-lap behind at staggered finish');eq(l.LapDifference(b[1],b[2]),0,'same-lap ahead at staggered finish');
 }
}

// 6.6.97 negative fixture: a parked car near either side of the line never
// regained trust, so both directions stayed neutral even after several laps.
{
 const old=algorithm(fs.readFileSync(path.join(here,'fixtures/RelativeLapTracker_v4153.cs.txt'),'utf8'),'RelativeLapTracker',['Reset','SetContext','Update','LapDifference']);old.Reset();
 const b=buffer();
 for(let tick=0;tick<=600;tick++){
  const v=1.98+tick*.004;car(b,0,2,.01,true);car(b,1,Math.floor(v)+1,v%1);
  old.SetContext(true,tick);old.Update(b);
  eq(old.LapDifference(b[0],b[1]),0,'old parked player blocks red indefinitely');
  eq(old.LapDifference(b[1],b[0]),0,'old parked opponent blocks blue indefinitely');
 }
}

// Recovered lap relationships before, during and after several overtakes.
// Both sides of start/finish, all CarIdx locations, multiple classes and
// cold start / teleport / telemetry dropout. The player's position NEVER
// changes during the encounter; leaving pits is not required to recover.
let repairedSamples=0;
for(const pct of [0,.001,.01,.029,.971,.99,.999,1])for(const player of [0,17,63])for(const reason of ['initial','teleport','dropout']){
 const b=buffer(),l=lapper(),other=(player+1)%64;
 car(b,player,2,reason==='initial'?pct:.20);
 const start=1+pct+.96;
 car(b,other,Math.floor(start)+1,start%1);
 b[player].ClassId=100;b[other].ClassId=200;
 l.SetContext(true,0);l.Update(b);
 if(reason==='dropout'){b[player].IsValid=false;l.SetContext(true,.1);l.Update(b);}
 for(let tick=0;tick<=2200;tick++){
  const v=start+tick*.0015,time=1+tick*.05;
  car(b,player,2,pct,true);car(b,other,Math.floor(v)+1,v%1);
  l.SetContext(true,time);l.Update(b);
  if(tick<60)continue;
  // Exactly opposite points have two equally short physical directions;
  // they are not a lapping encounter and have no unique rounded oracle.
  if(Math.abs(Math.abs(v-(1+pct)-Math.round(v-(1+pct)))-.5)<1e-8)continue;
  const expected=Math.round(v-(1+pct));
  eq(l.LapDifference(b[player],b[other]),expected,`parked red: pct=${pct} CarIdx=${player} reason=${reason} time=${time}`);
  eq(l.LapDifference(b[other],b[player]),-expected,'moving player sees blue for parked lapped car');
  repairedSamples++;
 }
}

// Even a player still on lap one must see a genuine approaching lapper.
// Practice and formation continue to suppress colors after anchor recovery.
for(const enabled of [false,true]){
 const b=buffer(),l=lapper();
 for(let tick=0;tick<=40;tick++){
  car(b,0,1,.01,true);car(b,1,2,.005);
  l.SetContext(enabled,tick*.1);l.Update(b);
 }
 eq(l.LapDifference(b[0],b[1]),enabled?1:0,'real lapper while player remains on lap one');
 l.Reset();
 for(let tick=0;tick<=40;tick++){
  car(b,0,1,.01);car(b,1,0,.995);
  l.SetContext(enabled,tick*.1);l.Update(b);
 }
 eq(l.LapDifference(b[0],b[1]),0,'staggered initial grid crossing remains neutral');
}

// An uncertain first sample while MOVING at the line must not be accepted
// solely because two seconds passed. Late/early raw counters retain the
// original finish-line protection until the coordinate is unambiguous.
for(const early of [false,true])for(const first of [false,true]){
 const b=buffer(),l=lapper();
 for(let tick=0;tick<1000;tick++){
  for(let i=0;i<3;i++){
   const v=(first?-.015:2.985)+tick*.0001+(i-1)*.006;
   car(b,i,Math.floor(v+(early?.003:-.003))+1,v-Math.floor(v));
  }
  l.SetContext(true,tick*.1);l.Update(b);
  eq(l.LapDifference(b[1],b[0]),0,'moving first sample at line: no false blue');
  eq(l.LapDifference(b[1],b[2]),0,'moving first sample at line: no false red');
 }
}

// Stable timestamps and counters are evidence; a frozen clock or a wobbling
// counter are not. Clock resets, raw changes and motion restart the window.
{
 const b=buffer(),l=lapper();car(b,0,2,.01,true);car(b,1,3,.20);
 for(let t=0;t<100;t++){l.SetContext(true,0);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),0,'repeated identical timestamp cannot restore trust');
 l.SetContext(true,2);l.Update(b);
 eq(l.LapDifference(b[0],b[1]),0,'two distinct samples are insufficient');
 l.SetContext(true,2.1);l.Update(b);
 eq(l.LapDifference(b[0],b[1]),1,'third distinct stable sample recovers');
 l.Reset();
 for(let t=0;t<50;t++){car(b,0,t%2?3:2,.01,true);l.SetContext(true,t*.1);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),0,'oscillating lap counter remains uncertain');
 car(b,0,2,.01,true);
 for(let t=0;t<=21;t++){l.SetContext(true,5+t*.1);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),1,'settled counter recovers without movement');
 l.Reset();
 for(let t=0;t<=30;t++){car(b,0,2,.01+t*.00001,true);l.SetContext(true,t*.1);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),0,'small cumulative movement is not stationary');
 for(let t=0;t<=22;t++){l.SetContext(true,4+t*.1);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),1,'stopping afterwards permits recovery');
 l.Reset();car(b,0,2,.01,true);
 for(const time of [100,101,0,1]){l.SetContext(true,time);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),0,'clock rewind restarts settling');
 l.SetContext(true,2);l.Update(b);
 eq(l.LapDifference(b[0],b[1]),1,'settles under new clock');
 for(const invalidTime of [-1,NaN,Infinity]){
  l.Reset();for(let n=0;n<10;n++){l.SetContext(true,invalidTime);l.Update(b);}
  eq(l.LapDifference(b[0],b[1]),0,'missing or invalid clock cannot fake stable evidence');
 }
}

// Raw scoring can arrive after the stationary anchor was acquired; reconcile
// that persistent correction in-zone as well (not only outside the guard).
{
 const b=buffer(),l=lapper();
 for(let t=0;t<=30;t++){car(b,0,2,.01,true);car(b,1,4,.02);l.SetContext(true,t*.1);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),2,'initial stationary anchor');
 for(let t=0;t<=30;t++){car(b,0,3,.01,true);l.SetContext(true,4+t*.1);l.Update(b);}
 eq(l.LapDifference(b[0],b[1]),1,'late persistent scoring correction reconciles in pits');
}

// Missing CarIdxLap can still use a valid completed counter. Small float32
// stationary jitter should not keep a pit stall permanently untrusted.
{
 const b=buffer(),l=lapper();
 for(let t=0;t<=40;t++){
  car(b,0,2,.01+(t%2?1:-1)*.000001,true);car(b,1,3,.005);
  b[0].Lap=-1;b[1].Lap=-1;
  l.SetContext(true,t*.1);l.Update(b);
 }
 eq(l.LapDifference(b[0],b[1]),1,'completed-counter fallback and small pit jitter recover red');
 eq(l.LapDifference(b[1],b[0]),-1,'completed-counter fallback and small pit jitter recover blue');
 b[0].LapCompleted=-1;l.SetContext(true,5);l.Update(b);
 eq(l.LapDifference(b[0],b[1]),0,'no valid lap counter remains neutral');
}

function* walk(v){if(!v||typeof v!=='object')return;if(Array.isArray(v)){for(const x of v)yield*walk(x);}else{yield v;for(const x of Object.values(v))yield*walk(x);}}
const dash=JSON.parse(execFileSync('unzip',['-p',path.join(root,'Overlays/Fulcrum_Relatives_v6.6.98_HIGHLIGHTS.simhubdash'),'*.djson'],{maxBuffer:30e6}).toString());
const nodes=[...walk(dash)].filter(n=>n.$type&&n.Name);
function run(name,key,props){const n=nodes.find(n=>n.Name===name);assert.ok(n,name);const e=n.Bindings?.[key]?.Formula?.Expression;return e===undefined?n[key]:new Function('$prop','root',e)(k=>props[k]??null,{});}
for(let player=1;player<=9;player++)for(let row=1;row<=9;row++){
 const P=`FulcrumPlugin.Fulcrum.Relative.Table.Row${String(row).padStart(2,'0')}.`, suffix=row-5;
 const props={[P+'IsPlayer']:row===player,[P+'Visible']:true};
 eq(run('Pos_Row_'+suffix,'BackgroundColor',props),row===player?'#00000000':'#C0143039','POS styling follows identity in all nine slots');
 for(const status of ['L1','L2','L9','OUT','PIT','--']){props[P+'StatusStintText']=status;eq(run('Status_Row_'+suffix,'Text',props),status,'dashboard does not subtract a lap');}
 for(const delta of [-1,0,1]){
  props[P+'IsLappedByPlayer']=delta<0;props[P+'IsAheadByLap']=delta>0;props[P+'IsSameClass']=false;
  eq(run('Name_Row_'+suffix,'TextColor',props),row===player?'#FFFFFFFF':delta<0?'#FF63C7FF':delta>0?'#FFFF4D57':'#FFEAF3F6','all-class lap colors remain wired');
 }
}
const palette=['#00000000','#FF1E6FE8','#FFE74C3C','#FFF2C94C','#FF22A06B','#FF8B5CF6','#FFF08C2E','#FFE052A0'];
for(let row=1;row<=9;row++)for(const player of [true,false])for(const status of ['', 'PIT'])for(let c=0;c<palette.length;c++){
 const P=`FulcrumPlugin.Fulcrum.Relative.Table.Row${String(row).padStart(2,'0')}.`;
 const props={[P+'IsPlayer']:player,[P+'Status']:status,[P+'ClassColorSlot']:c};
 eq(run('No_Row_'+(row-5),'BackgroundColor',props),palette[c],'class badge survives player identity and pit status');
}
for(const n of walk(dash))if(typeof n.Expression==='string'&&Number(n.Interpreter)===1){new Function(n.Expression);checks++;}
const mod=fs.readFileSync(path.join(kit,'Fulcrum.Plugin/Modules/RelativeModule.cs'),'utf8');
const context=body(mod,'UpdateRelativeRaceContext');
ok(context.includes('RelativeSessionReader.State(latestRawData)'),'production uses nested reader');
ok(context.includes('stintTracker.Reset()'),'production resets stints across sessions');
ok(mod.indexOf('UpdateRelativeRaceContext();')<mod.indexOf('stintTracker.Update('),'reset/context occur before stint update');
ok(context.includes('classPositions.Update(participantBuffer, sessionDatabase, isRace, state, latestRawData)'),'production passes source grid data');
ok(context.includes('SetLapColorContext(isRace && state >= 4 && state <= 6, time)'),'production enables lap colors from resolved race state');
console.log(JSON.stringify({status:'PASS',checks,repairedSamples,scope:'real dashboard JS and source-derived C# logic with BCL/reflection shims; native compiled pipeline runs in Windows BAT'},null,2));
export {algorithm, src, buffer, car, lapper, Reader, body, subset};
