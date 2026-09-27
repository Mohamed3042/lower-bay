/** Drive the actual preview controller through authored routes and train motion. */
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createCollisionWorld,createPlayer,stepPlayer,moveColliderGroup,playerIntersects,V2_PLAYER} from '../lib/physics.js';
import {trainOffset,sampleTrainMotion} from '../lib/dynamics.js';

const [mapPath,reportPath]=process.argv.slice(2);
if(!mapPath||!reportPath)throw new Error('Usage: node prove_runtime_walk.mjs map.json new-report.json');
if(fs.existsSync(reportPath))throw new Error('Receipt exists: choose a new output path.');
const bytes=fs.readFileSync(mapPath),map=JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/,''));
const tick=1/60,worldFor=()=>createCollisionWorld(map.entities,{profile:V2_PLAYER});
function walkRoute(route,reverse=false){
  const points=reverse?[...route.points].reverse():route.points;
  const world=worldFor(),player=createPlayer(points[0]),segments=[];
  let frames=0,jumps=0;
  for(let index=1;index<points.length;index++){
    const target=points[index],start={...player};let stuck=0,lastJump=-120,passed=false;
    const maximum=Math.ceil((Math.hypot(target[0]-player.x,target[2]-player.z)/V2_PLAYER.walkSpeed+8)*60);
    for(let frame=0;frame<maximum;frame++){
      const dx=target[0]-player.x,dz=target[2]-player.z,distance=Math.hypot(dx,dz);
      if(distance<.1&&Math.abs(player.y-target[1])<.12){passed=true;break;}
      const previous={...player};
      const jump=stuck>24&&frame-lastJump>90&&player.grounded;
      if(jump){jumps++;lastJump=frame;stuck=0;}
      const scale=Math.min(1,distance/(V2_PLAYER.walkSpeed*tick));
      stepPlayer(player,{x:distance>.03?dx/distance*scale:0,z:distance>.03?dz/distance*scale:0,jump},world,tick);
      frames++;
      if(Math.hypot(player.x-previous.x,player.z-previous.z)<.0001)stuck++;else stuck=0;
      if(player.y < -10)break;
    }
    segments.push({target,start:[start.x,start.y,start.z],end:[player.x,player.y,player.z],passed,intersects:playerIntersects(world,player)});
    if(!passed)break;
  }
  return {id:route.id,direction:reverse?'reverse':'forward',passed:segments.length===points.length-1&&segments.every(s=>s.passed&&!s.intersects),simulationSeconds:frames/60,jumps,segments};
}
function trainProof(dynamic){
  const world=worldFor(),colliders=world.filter(c=>dynamic.entityIds.includes(c.id));
  const actor=map.pickups.find(p=>dynamic.actorIds?.includes(p.id));
  if(!actor)return {id:dynamic.id,passed:false,error:'No carried pickup to check'};
  const axisIndex=dynamic.axis==='x'?0:2;
  const startOffset=trainOffset(dynamic,0),start=[...actor.position];start[1]-=.8;start[axisIndex]+=startOffset;
  moveColliderGroup(world,colliders,dynamic.axis,0,startOffset);
  const player=createPlayer(start);let previous=startOffset,reset=null,disembark=null,carriedFrames=0,maximumCarryError=0,maximumPickupOffsetError=0;
  const period=sampleTrainMotion(dynamic,0).period;
  const snapshots=[];
  for(let frame=1;frame<=Math.ceil(period*60);frame++){
    const seconds=frame*tick,next=trainOffset(dynamic,seconds);
    const result=moveColliderGroup(world,colliders,dynamic.axis,previous,next,player,{hazard:dynamic.hazard});
    if(result.carried)carriedFrames++;
    if(result.disembarked&&!disembark)disembark={seconds,surface:result.disembarked,position:[player.x,player.y,player.z]};
    if(result.reset){reset={seconds,...result,position:[player.x,player.y,player.z]};break;}
    stepPlayer(player,{},world,tick);
    const actorPosition=[...actor.position];actorPosition[axisIndex]+=next;
    if(!disembark)maximumCarryError=Math.max(maximumCarryError,Math.abs(player[dynamic.axis]-(start[axisIndex]+next-startOffset)));
    maximumPickupOffsetError=Math.max(maximumPickupOffsetError,Math.abs((actorPosition[axisIndex]-actor.position[axisIndex])-next));
    if(frame%60===0)snapshots.push({seconds,offset:next,rider:[player.x,player.y,player.z],pickup:actorPosition});
    previous=next;
  }
  // A whole-map body-contact proof uses the same actual train colliders, with a
  // stationary capsule ahead of its departure end on the track bed.
  const hazardWorld=worldFor(),hazardColliders=hazardWorld.filter(c=>dynamic.entityIds.includes(c.id));
  const leading=Math.max(...hazardColliders.map(c=>c[dynamic.axis]+(dynamic.axis==='x'?c.hx:c.hz)));
  const victim=createPlayer(dynamic.axis==='x'?[leading+1,-1.2,actor.position[2]]:[actor.position[0],-1.2,leading+1]);
  const hazard=moveColliderGroup(hazardWorld,hazardColliders,dynamic.axis,0,Math.min(dynamic.max,3),victim,{hazard:dynamic.hazard});
  // The source catwalk has a committed lip, not the former ascent wedge.
  // Carrying an idle rider into that lip must request a trapped-player reset.
  const resetPlayer=createPlayer(map.spawns[0].position.map((v,i)=>i===1?v-1.1:v));
  const safeReset=reset?.reason==='trapped'&&Math.abs(player.x-18)<1&&Math.abs(player.y-2.2)<.05&&!playerIntersects(world,resetPlayer);
  const fullCycle=Array.from({length:121},(_,i)=>{const seconds=period*i/120;const offset=trainOffset(dynamic,seconds);return {seconds,offset,pickupX:actor.position[0]+offset};});
  return {id:dynamic.id,passed:safeReset&&carriedFrames>0&&maximumCarryError<.001&&maximumPickupOffsetError<.001&&hazard.reset,periodSeconds:period,carriedFrames,maximumCarryError,maximumPickupOffsetError,disembark,reset,safeReset,resetPosition:[resetPlayer.x,resetPlayer.y,resetPlayer.z],hazard,snapshots,fullCycle,note:'The restored 0.8m committed catwalk lip blocks a carried rider. The controller requests a trapped-player reset to a clear spawn; the packaged Unity proof separately exercises the actual reset. Full-cycle train and pickup offsets are sampled after the rider reset.'};
}
const routes=(map.routes||[]).flatMap(route=>[walkRoute(route),...(route.direction==='both'?[walkRoute(route,true)]:[])]);
const dynamics=(map.dynamics||[]).map(trainProof);
const report={map_id:map.id,map_sha256:crypto.createHash('sha256').update(bytes).digest('hex'),controller:'src/physics.js stepPlayer + moveColliderGroup',profile:V2_PLAYER,timeStep:tick,scope:'Deterministic execution of the browser movement controller; authored static route pose, full-cycle moving train rider/pickup and body-contact behavior. Native Unity player acceptance is separate.',passed:routes.every(r=>r.passed)&&dynamics.every(d=>d.passed),routes,dynamics};
fs.mkdirSync(path.dirname(reportPath),{recursive:true});fs.writeFileSync(reportPath,JSON.stringify(report,null,2));
console.log(JSON.stringify({passed:report.passed,map_sha256:report.map_sha256,routes:routes.map(({id,direction,passed,jumps,segments})=>({id,direction,passed,jumps,failed:segments.filter(s=>!s.passed)})),dynamics:dynamics.map(({snapshots,...rest})=>rest)},null,2));
if(!report.passed)process.exitCode=1;
