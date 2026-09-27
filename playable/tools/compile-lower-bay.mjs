import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {compileScenePlan} from '../lib/scene-plan.js';

// Reproducible, explicit reconstruction. Dimensions are evidence-informed estimates,
// not geometry recovered by the pixel palette analyser.
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const base=root;
const entities=[], modules=[], trainIds=[];
const tile='textures/ivory-tile-albedo.png',concrete='textures/concrete-albedo.png',sign='textures/lower-bay-sign.png',poster='textures/winter-poster.png',steel='textures/train-steel-albedo.png';
const matTile={texture:tile,roughness:.88,uvScale:[1,1]};
const matFloor={texture:concrete,roughness:.96,uvScale:[12,2]};
function box(id,position,size,color='#ffffff',options={}){
  const e={id,kind:'wall',position,size,rotation:[0,0,0],color,collidable:true,walkable:false,...options};entities.push(e);return e;
}
function floor(id,x,z,w,d,y=0,options={}){return box(id,[x,y-.15,z],[w,.3,d],'#ffffff',{kind:'floor',walkable:true,material:{...matFloor,uvScale:[w/4,d/4]},...options});}
function wall(id,x,z,w,d,h,y=0,options={}){
  box(id,[x,y+h/2,z],[w,h,d],'#ffffff',{material:{...matTile,uvScale:[Math.max(w,d)/3,h/3]},...options});
  box(id+'-dado',[x,y+.4,z],[w+.025,.8,d+.025],'#2f3231',{collidable:false,...options,material:{roughness:.98}});
}
function roof(id,x,z,w,d,y){box(id,[x,y+.15,z],[w,.3,d],'#ccccbf',{roof:true,material:{...matFloor,uvScale:[w/4,d/4]}});}
function glow(id,position,size,color='#dbe7d0',intensity=1.8){return box(id,position,size,color,{kind:'decoration',collidable:false,material:{emissive:color,emissiveIntensity:intensity,roughness:.35}});}
function ramp(id,x,z,w,d,bottom,top,yaw=0){return box(id,[x,(bottom+top)/2,z],[w,top-bottom,d],'#ffffff',{kind:'ramp',walkable:true,rotation:[0,yaw,0],material:{...matFloor,uvScale:[w/3,d/3]}});}

// One track with two platforms, wide glass concourse, narrow solid-wall flank.
floor('track-bed',0,0,132,4,-1.2,{zone:'track'});
for(const side of [-1,1])floor('platform-'+side,0,side*4,76,4,0,{zone:side>0?'north-platform':'south-platform'});
// The exposed trench-side platform riser closes the gap beneath each slab;
// leave the four access openings for the explicit wedges below.
for(const side of [-1,1])for(const [left,right] of [[-38,-27.4],[-24.6,24.6],[27.4,38]]){
  box(`platform-fascia-${side}-${left}`,[(left+right)/2,-.75,side*2.08],[right-left,.9,.16],'#6e736c',{material:{...matTile,uvScale:[(right-left)/3,.3]}});
}
floor('concourse',0,11,76,10,0,{zone:'concourse'});
floor('flank-corridor',0,-7.4,76,2.8,0,{zone:'flank'});
floor('ad-alcove',0,-10.55,14,3.5,0,{zone:'alcove'});
// Canonical source end-door intervals reflected into Unity X. The former two
// extra middle entrances are removed; the flank is an end-entered route again.
const flankDoors=[[-29.64,-25.08],[29.64,34.2]];
let flankEdge=-38;
for(const [i,[left,right]] of flankDoors.entries()){
  wall('south-divider-'+i,(flankEdge+left)/2,-6,left-flankEdge,.28,3.5);
  wall('south-door-header-'+i,(left+right)/2,-6,right-left,.28,1.1,2.4);
  flankEdge=right;
}
wall('south-divider-end',(flankEdge+38)/2,-6,38-flankEdge,.28,3.5);
// Leave source-positioned service-room footprints open through the outer wall.
wall('south-exterior-left',-21,-8.95,28,.3,3.5);
wall('south-exterior-right',21,-8.95,28,.3,3.5);
wall('alcove-rear',0,-12.2,14.6,.3,3.5);
wall('alcove-side-left',-7.15,-10.6,.3,3.2,3.5);
wall('alcove-side-right',7.15,-10.6,.3,3.2,3.5);
wall('concourse-back',0,16.15,76.6,.3,3.9);

// Exactly five glass connections. Three inner centers use canonical fractions
// mirrored into Unity; outer openings follow verified upper-ramp approaches.
const glassDoors=[{x:-31.6,w:4,h:3.35},{x:-12.16,w:3,h:2.4},{x:1.52,w:3,h:2.4},{x:15.2,w:3,h:2.4},{x:31.6,w:4,h:3.35}];
const glassMaterial={opacity:.24,roughness:.15,metalness:.1};
let glassEdge=-38;
for(const [i,door] of glassDoors.entries()){
  const left=door.x-door.w/2,right=door.x+door.w/2,w=left-glassEdge,x=(left+glassEdge)/2;
  if(i===0){
    box('glass-0-under-gallery',[-36.6,1.55,6],[2.8,3.1,.1],'#b3c7bf',{material:glassMaterial});
    box('glass-0',[-34.4,1.9,6],[1.6,3.8,.1],'#b3c7bf',{material:glassMaterial});
    for(const y of [.12,3.78])box(`glass-rail-${i}-${y}`,[-34.4,y,6],[1.6,.1,.16],'#353c3c');
  }else{
    box('glass-'+i,[x,1.9,6],[w,3.8,.1],'#b3c7bf',{material:glassMaterial});
    for(const y of [.12,3.78])box(`glass-rail-${i}-${y}`,[x,y,6],[w,.1,.16],'#353c3c');
  }
  for(const edge of [left,right])box(`glass-door-post-${i}-${edge}`,[edge,1.9,6],[.08,3.8,.15],'#343c3d');
  box('glass-door-header-'+i,[door.x,(door.h+3.8)/2,6],[door.w,3.8-door.h,.1],'#b3c7bf',{material:glassMaterial});
  glassEdge=right;
}
box('glass-end',[34.4,1.9,6],[1.6,3.8,.1],'#b3c7bf',{material:glassMaterial});
box('glass-end-under-gallery',[36.6,1.55,6],[2.8,3.1,.1],'#b3c7bf',{material:glassMaterial});

// Repeatable tile pier modules:36 hall piers and22 concourse piers.
const pier=(height,width)=>({entities:[
  {id:'tile',kind:'wall',position:[0,height/2,0],size:[width,height,width],rotation:[0,0,0],color:'#ffffff',material:{...matTile,uvScale:[width/2,height/3]}},
  {id:'plinth',kind:'decoration',position:[0,.4,0],size:[width+.03,.8,width+.03],rotation:[0,0,0],color:'#303734',collidable:false},
  {id:'capital',kind:'decoration',position:[0,height-.1,0],size:[width+.16,.2,width+.16],rotation:[0,0,0],color:'#aaa998',collidable:false}
]});
for(const side of [-1,1])modules.push({id:'hall-piers-'+side,template:'hall-pier',position:[-34,0,side*4.9],repeat:{count:18,step:[4,0,0]},zone:'hall'});
for(const z of [9,13])modules.push({id:'concourse-piers-'+z,template:'concourse-pier',position:[-30,0,z],repeat:{count:11,step:[6,0,0]},zone:'concourse'});

for(let x=-36;x<=36;x+=6){
  for(const z of [-3.9,3.9]){box(`light-backing-${x}-${z}`,[x,5.25,z],[2.5,.16,.36],'#303331',{collidable:false});glow(`tube-${x}-${z}`,[x,5.12,z],[2.3,.07,.22]);}
  for(const z of [8,14])glow(`concourse-tube-${x}-${z}`,[x,3.72,z],[1.8,.06,.18]);
  glow('flank-light-'+x,[x,3.32,-7.4],[.5,.08,.25],'#d6dabc',1.2);
}
for(const z of [-2.1,2.1])box('platform-safety-'+z,[0,.014,z],[76,.026,.18],'#cab95a',{kind:'decoration',collidable:false});
// Rail tops are real 0.21m steps above the trench, below the 0.4m capsule step limit.
for(const z of [-.72,.72])box('rail-'+z,[0,-1.09,z],[132,.2,.075],'#777d7b',{walkable:true,material:{metalness:.8,roughness:.5}});
for(let x=-65;x<=65;x+=1.6)box('sleeper-'+x,[x,-1.145,0],[.18,.11,1.95],'#342f2b',{collidable:false});
// Inferred access wedges meet the actual platform lips at Z=±2. The earlier
// wedges ended inside the slabs, trapping the standing capsule halfway up.
for(const x of [-26,26]){ramp('track-access-south-'+x,x,-1,2.8,2,-1.2,0,180);ramp('track-access-north-'+x,x,1,2.8,2,-1.2,0);}

// Upper spawn rooms and their continuous ramp→landing→return gallery.
// Exact turns beyond the filmed doorways are estimated and documented.
for(const s of [-1,1]){
  const side=s<0?'red':'blue';
  floor(side+'-spawn-floor',s*36,0,8,8,3.4,{zone:side+'-spawn'});
  // Meet the existing inner partition at |X|=34; the former 4.8m slab
  // left a fall-through strip between its edge and the partition.
  floor(side+'-upper-return',s*37,8.5,6,9,3.4,{zone:side+'-upper'});
  floor(side+'-upper-landing',s*34.8,14,10.4,3,3.4,{zone:side+'-upper'});
  ramp(side+'-main-ramp',s*31.6,8.25,3.2,8.5,0,3.4);
  // Low platform-end approach connects the ramps to the main platforms.
  floor(side+'-ramp-approach',s*33,4.8,6,1.6,0);
  // Source gameplay: open catwalk tips at X=+/-18 require a committed 0.8m drop.
  floor(side+'-catwalk',s*24,0,12,2.6,3,{zone:side+'-catwalk'});
  ramp(side+'-spawn-catwalk-ramp',s*31,0,2.6,2,3,3.4,s*90);
  wall(side+'-spawn-back',s*40.15,0,.3,8.3,3.9,3.4);
  wall(side+'-spawn-south',s*36,-4.15,8.3,.3,3.9,3.4);
  wall(side+'-gallery-outer',s*40.15,9.8,.3,11.3,3.9,3.4);
  wall(side+'-landing-end',s*34.8,15.65,10.8,.3,3.9,3.4);
  // The inner partition ends before the upper landing, leaving a turn wider than1m.
  wall(side+'-gallery-divider',s*34,8.1,.24,7.8,3.6,3.4);
  wall(side+'-spawn-front-left',s*32,-2.75,.28,2.5,3.9,3.4);
  wall(side+'-spawn-front-right',s*32,2.75,.28,2.5,3.9,3.4);
  wall(side+'-spawn-front-header',s*32,0,.28,3,1.5,5.8);
  wall(side+'-spawn-north-return',s*33,4,2,.24,3.9,3.4);
  for(const z of [-1.28,1.28]){
    box(side+'-catwalk-railing-'+z,[s*24,3.85,z],[12,.12,.09],'#555a58');
    for(let x=18;x<=30;x+=2)box(`${side}-railpost-${x}-${z}`,[s*x,3.42,z],[.08,.84,.08],'#555a58');
  }
  // Main hall end portals leave track opening clear beneath the upper rooms.
  wall(side+'-end-wall-south',s*38,-4.7,.3,2.3,3.1);
  wall(side+'-end-wall-north',s*38,5,.3,2,3.3);
  wall(side+'-concourse-end',s*38,10,.3,8,3.2);
  wall(side+'-flank-end',s*38,-7.5,.3,2.8,3.5);
  box(side+'-lift-door',[s*39.98,4.55,0],[.035,2.3,2.5],'#636864',{material:{metalness:.45,roughness:.7}});
  // Source-backed semicircular fanlight, represented by vertical static strips.
  // Radius/base are explicitly adapted to the current room's door and ceiling.
  const fanRadius=1.4,fanBase=5.75,fanStep=fanRadius*2/24;
  for(let pane=0;pane<24;pane++){
    const z=-fanRadius+(pane+.5)*fanStep,h=Math.sqrt(fanRadius*fanRadius-z*z);
    glow(`fanlight-${side}-pane-${pane}`,[s*39.948,fanBase+h/2,z],[.018,h,fanStep*.94],'#ceded9',1.2);
    box(`fanlight-${side}-arch-${pane}`,[s*39.925,fanBase+h+.065,z],[.07,.13,fanStep*1.1],'#d3d0ba',{collidable:false,material:matTile});
    if(pane%3===0)box(`fanlight-${side}-mullion-${pane}`,[s*39.926,fanBase+h/2,z],[.04,h,.027],'#454d49',{collidable:false});
  }
  box(`fanlight-${side}-sill`,[s*39.92,fanBase-.04,0],[.1,.08,fanRadius*2+.18],'#b6b7a4',{collidable:false});
  glow(side+'-spawn-light',[s*36,7.13,0],[3,.08,.35],s<0?'#e0b7af':'#afceda',1.4);
  for(const z of [6,10,14])glow(side+'-upper-light-'+z,[s*37.5,7.1,z],[1.1,.06,.25]);
  roof(side+'-spawn-roof',s*36,0,8.4,8.6,7.3);
  roof(side+'-gallery-roof',s*37,8.7,6.5,9.4,7.3);
  roof(side+'-landing-roof',s*34.8,14,10.6,3.2,7.3);
  // Enclose the rising volume between the low concourse ceiling and the upper
  // gallery. These planes meet the existing ceilings without coplanar overlap.
  roof(side+'-ramp-roof',s*31.625,8.2,4.25,8.4,7.3);
  wall(side+'-ramp-concourse-header',s*29.65,9.3,.3,6.6,3.4,3.9);
  wall(side+'-ramp-hall-header',s*30.75,4,2.5,.2,1.8,5.5);
}
// Tunnel runout extended symmetrically to contain the moving two-car train.
for(const s of [-1,1]){
  for(const z of [-2.25,2.25]){
    wall(`tunnel-wall-${s}-${z}`,s*53,z,26,.4,6.7,-1.2,{material:{...matFloor,uvScale:[6.5,2]}});
    // Under-room portals stop at the deck underside instead of splitting the
    // occupied upper room with full-height tunnel walls.
    wall(`tunnel-under-room-${s}-${z}`,s*39,z,2,.4,4.3,-1.2,{material:{...matFloor,uvScale:[.5,1.5]}});
  }
  wall('tunnel-stop-'+s,s*66.2,0,.4,4.8,6.7,-1.2,{material:{...matFloor,uvScale:[1,2]}});
  roof('tunnel-roof-'+s,s*52,0,28.4,4.9,5.5);
  for(let x=42;x<66;x+=6)glow(`tunnel-lamp-${s}-${x}`,[s*x,4.9,0],[.5,.1,.5],'#d8ae78',1.1);
}
roof('hall-roof',0,0,64,12,5.5);
roof('concourse-roof',0,11,59,10,3.9);
roof('flank-roof',0,-7.5,76,3.2,3.5);
roof('alcove-roof',0,-10.55,14.6,3.5,3.5);

// Canonical source positions reflected into Unity X. Shell thicknesses, clear
// doorways and interiors are authored estimates, never claimed as recovered mesh.
for(let index=0;index<12;index++){
  const x=11-index*2;
  box(`fare-gate-${index}-body`,[x,.525,12.5],[.35,1.05,1.7],'#686f68',{zone:'fare-gates',material:{metalness:.55,roughness:.45}});
  glow(`fare-gate-${index}-top`,[x,1.08,12.5],[.35,.06,1.7],'#94ad9d',.2);
  box(`fare-gate-${index}-reader`,[x,1.13,11.86],[.24,.045,.21],'#182e2b',{collidable:false});
}
for(const x of [-13.2,13.2])box('fare-end-post-'+x,[x,1.05,12.5],[.6,2.1,.5],'#727567',{zone:'fare-gates'});
for(let index=0;index<4;index++){
  const x=21-index*1.5;
  box(`ticket-machine-${index}-body`,[x,1.15,15.2],[1.0,2.3,.72],'#5b6664',{zone:'ticket-machines',material:{metalness:.5,roughness:.5}});
  glow(`ticket-machine-${index}-screen`,[x,1.5,14.831],[.68,.68,.018],'#b9c7af',.35);
  box(`ticket-machine-${index}-tray`,[x,.69,14.79],[.64,.12,.1],'#29332e',{collidable:false});
  box(`ticket-machine-${index}-header`,[x,2.09,14.824],[.75,.18,.024],'#e0dbb8',{collidable:false,label:'TICKETS'});
}
// Openable booth interior around the existing source pier: entry on its west side.
wall('info-booth-back',-7,14.8,3.4,.16,2.4);
wall('info-booth-east',-5.3,13.6,.16,2.4,2.4);
wall('info-booth-west-front',-8.7,12.7,.16,.6,2.4);
box('info-booth-counter',[-7,.53,12.4],[3.4,1.06,.22],'#73796d',{zone:'info-booth'});
box('info-booth-window',[-7,1.66,12.4],[3.4,1.06,.04],'#b8cdc3',{material:glassMaterial});
roof('info-booth-canopy',-7,13.6,3.8,2.8,2.45);
glow('info-booth-sign',[-7,2.2,12.285],[2.7,.26,.018],'#c8d2bd',.3);

// Source restroom envelope X=24..31 (OBJ), Z=11.5..15.5; reflected here.
// A service doorway keeps the under-gallery pocket connected after the five
// glass doors close its former exit. This opening is a traversal adaptation;
// the room envelope remains source-anchored and its hidden door is inferred.
wall('restroom-west-front',-31,12.425,.2,1.85,2.6);
wall('restroom-west-back',-31,15.225,.2,.55,2.6);
wall('restroom-service-header',-31,14.15,.2,1.6,.3,2.3);
wall('restroom-east',-24,13.5,.2,4,2.6);
wall('restroom-back',-27.5,15.5,7,.2,2.6);
wall('restroom-front-left',-29.65,11.5,2.7,.2,2.6);
wall('restroom-front-right',-25.35,11.5,2.7,.2,2.6);
wall('restroom-door-header',-27.5,11.5,1.6,.2,.3,2.3);
roof('restroom-roof',-27.5,13.5,7.2,4.2,2.6);
for(const x of [-28.6,-26.3]){
  box('restroom-basin-'+x,[x,.84,15.04],[.7,.28,.58],'#c2c6b7',{zone:'restroom'});
  box('restroom-mirror-'+x,[x,1.72,15.365],[.8,.85,.025],'#84948e',{collidable:false,material:{metalness:.7,roughness:.25}});
}
glow('restroom-light',[-27.5,2.52,13.4],[1.4,.055,.18],'#cfdbcb',1.0).zone='restroom';

for(const s of [-1,1]){
  const id=s<0?'service-west':'service-east',x=s*36.5;
  // Extra floor only outside the original corridor, avoiding duplicate planes.
  floor(id+'-floor',x,-9.8,3,2,0,{zone:id});
  wall(id+'-back',x,-10.8,3,.2,2.8);
  wall(id+'-inner-side',s*35,-9.4,.2,2.8,2.8);
  wall(id+'-outer-side',s*38,-9.85,.2,1.9,2.8);
  for(const dx of [-1.175,1.175])wall(id+'-front-'+dx,x+dx,-8,.65,.2,2.8);
  wall(id+'-door-header',x,-8,1.7,.2,.5,2.3);
  roof(id+'-roof',x,-9.4,3.2,3,2.8);
  box(id+'-electrical-cabinet',[x,1.25,-10.5],[1.1,2.2,.4],'#657065',{zone:id});
  glow(id+'-status',[x,1.5,-10.29],[.6,.32,.025],'#91af8a',.2);
  glow(id+'-light',[x,2.71,-8.8],[.8,.05,.18],'#ced4ac',1.2).zone=id;
}

// Detailed two-car train, shared runtime group and attached roof sniper.
for(const [car,x] of [[0,-9],[1,9]]){
  const add=(suffix,p,size,color,options={})=>{const id=`train-${car}-${suffix}`;trainIds.push(id);return box(id,p,size,color,options);};
  add('body',[x,.64,0],[17.8,3.12,2.9],'#858c87',{kind:'platform',walkable:true,material:{texture:steel,uvScale:[6,1],metalness:.3,roughness:.72}});
  add('roof',[x,2.215,0],[17.85,.03,2.94],'#808983',{collidable:false,material:{texture:steel,uvScale:[6,1],metalness:.25,roughness:.76}});
  add('chassis',[x,-.96,0],[16.7,.17,2.4],'#282c2b',{collidable:false});
  for(const side of [-1,1]){
    add('stripe-'+side,[x,.25,side*1.459],[17.6,.18,.025],'#283744',{collidable:false});
    for(let p=-7;p<=7;p+=2){
      add(`window-frame-${side}-${p}`,[x+p,1.2,side*1.465],[1.65,1.1,.055],'#303735',{collidable:false});
      add(`window-${side}-${p}`,[x+p,1.2,side*1.5],[1.49,.94,.025],'#c4d3ba',{collidable:false,material:{emissive:'#c4d3ba',emissiveIntensity:.45,roughness:.28}});
    }
    for(const p of [-5,5]){
      add(`door-${side}-${p}`,[x+p,.6,side*1.52],[1.2,2.7,.06],'#585f5b',{collidable:false});
      for(const d of [-.27,.27])add(`door-light-${side}-${p}-${d}`,[x+p+d,1.04,side*1.56],[.34,1.65,.025],'#d4ddbf',{collidable:false,material:{emissive:'#d4ddbf',emissiveIntensity:.6}});
    }
  }
  for(const xOff of [-8.91,8.91])add('cab-'+xOff,[x+xOff,1.15,0],[.025,1,2.45],'#384641',{collidable:false,material:{roughness:.22}});
}

for(const x of [-24,0,24])for(const z of [-5.8,5.8]){
  box(`sign-${x}-${z}`,[x,3.45,z],[6.6,1.5,.055],'#ffffff',{collidable:false,material:{texture:sign,roughness:.8,uvScale:[1,1]},label:'Gideons Tower ← LowerBay → Fort Winter'});
}
for(const x of [-3.5,3.5]){
  box('poster-frame-'+x,[x,1.75,-11.97],[4.05,2.3,.08],'#35352e',{collidable:false});
  box('poster-'+x,[x,1.75,-11.91],[3.9,2.195,.03],'#ffffff',{collidable:false,label:'Winter travel artwork: AI interpretation of video 3 at 00:25',material:{texture:poster,roughness:.82}});
}
box('vending-case',[-6.2,1.25,-10.8],[1.1,2.5,1.2],'#843c38');
glow('vending-panel',[-6.2,1.55,-10.18],[.85,1.7,.03],'#acb79c',.5);
for(const x of [-20,20])box('bench-'+x,[x,.38,15.25],[3,.76,.7],'#62634f');

const spawns=[];for(const s of [-1,1])for(const z of [-2,0,2])spawns.push({id:(s<0?'red':'blue')+'-spawn-'+z,team:s<0?'red':'blue',position:[s*36,4.5,z],yaw:s<0?90:270});
const pickups=[
  {id:'roof-sniper',kind:'sniper',position:[0,3,0]},
  {id:'alcove-heavy-armor',kind:'heavy-armor',position:[0,.8,-10.2]},
  {id:'concourse-armor',kind:'armor',position:[0,.8,11]},
  ...[-1,1].flatMap(s=>[
    {id:'concourse-health-'+s,kind:'health',position:[s*28,.8,8]},
    {id:'track-health-'+s,kind:'health',position:[s*24,-.4,0]},
    {id:'flank-ammo-'+s,kind:'ammo',position:[s*30,.8,-7.4]},
    {id:'platform-ammo-'+s,kind:'ammo',position:[s*8,.8,s*4]},
  ])
];
const zones=[['track','Sunken single track',-1.2],['north-platform','Glass-side platform',0],['south-platform','Solid-side platform',0],['concourse','Pillared glass concourse',0],['flank','Narrow flank corridor',0],['alcove','Advertisement / heavy armor alcove',0],['red-spawn','Red upper room',3.4],['blue-spawn','Blue upper room',3.4],['red-upper','Red upper return',3.4],['blue-upper','Blue upper return',3.4],['red-catwalk','Red dead-end catwalk',3],['blue-catwalk','Blue dead-end catwalk',3]].map(([id,name,floorY])=>({id,name,floorY,confidence:id.includes('upper')?'inferred':'observed'}));
for(const [id,name] of [['fare-gates','Fare gate bank'],['ticket-machines','Ticket machine bank'],['info-booth','Information booth interior'],['restroom','Restroom interior'],['service-west','West electrical room'],['service-east','East electrical room']])zones.push({id,name,floorY:0,confidence:'inferred'});
const routes=[
  {id:'north-length',from:'north-platform',to:'north-platform',points:[[-29,0,3.5],[0,0,3.5],[29,0,3.5]]},
  {id:'south-length',from:'south-platform',to:'south-platform',points:[[-29,0,-3.5],[0,0,-3.5],[29,0,-3.5]]},
  {id:'concourse-length',from:'concourse',to:'concourse',points:[[-28,0,10.5],[0,0,10.5],[28,0,10.5]]},
  {id:'flank-length',from:'flank',to:'flank',points:[[-30,0,-7.4],[0,0,-7.4],[30,0,-7.4]]},
  ...[-26,26].map(x=>({id:'track-crossing-'+x,from:'south-platform',to:'north-platform',points:[[x,0,-3.4],[x,0,-2],[x,-1.2,0],[x,0,2],[x,0,3.4]]})),
  ...glassDoors.map((door,index)=>({id:'glass-entrance-'+index,from:'north-platform',to:index===0?'red-upper':index===4?'blue-upper':'concourse',points:index===0||index===4?[[door.x,0,4],[door.x,.8,6],[door.x,3.4,12.5]]:[[(index===2?.8:door.x),0,3.5],[(index===2?.8:door.x),0,8]]})),
  ...flankDoors.map(([left,right],index)=>({id:'flank-entrance-'+index,from:'south-platform',to:'flank',points:[[(left+right)/2,0,-3.5],[(left+right)/2,0,-7]]})),
  {id:'fare-gate-passage',from:'concourse',to:'fare-gates',points:[[2,0,10.5],[2,0,14.2]]},
  {id:'ticket-machines-access',from:'concourse',to:'ticket-machines',points:[[15,0,10.5],[15,0,14],[21,0,14]]},
  {id:'info-booth-access',from:'concourse',to:'info-booth',points:[[-15,0,10.5],[-15,0,14.2],[-9.8,0,14.2],[-9.8,0,13.9],[-7.6,0,13.9]]},
  {id:'restroom-access',from:'concourse',to:'restroom',points:[[-27.5,0,10.5],[-27.5,0,14]]},
  {id:'red-under-gallery-return',from:'concourse',to:'concourse',points:[[-36,0,8],[-35,0,13.5],[-33,0,14.15],[-29.2,0,14.15],[-27.5,0,14],[-27.5,0,10.5]]},
  ...[-1,1].map(s=>({id:(s<0?'service-west':'service-east')+'-access',from:'flank',to:s<0?'service-west':'service-east',points:[[s*31,0,-7],[s*33.7,0,-6.7],[s*36.5,0,-6.7],[s*36.5,0,-9.2]]})),
  ...[-1,1].map(s=>({id:(s<0?'red':'blue')+'-upper-return-route',from:s<0?'red-spawn':'blue-spawn',to:'north-platform',points:[[s*36,3.4,0],[s*37.6,3.4,6],[s*37.6,3.4,14],[s*31.6,3.4,14],[s*31.6,3.4,12.5],[s*31.6,0,4],[s*29,0,3.5]]})),
  {id:'roof-sniper-access',from:'red-spawn-0',to:'roof-sniper',points:[[-36,3.4,0],[-32,3.4,0],[-30,3,0],[-18,3,0],[-17,2.2,0],[0,2.2,0]]}
].map(r=>({...r,direction:r.id==='roof-sniper-access'?'forward':'both',evidence:r.id.includes('upper')?'Visible ramp/upper passage topology; exact return length and turns inferred.':'Source specification plus visual references; tested at authored static train pose.'}));
const plan={id:'lower-bay-reconstruction-v2',name:'Lower Bay — Reconstruction',seed:'lower-bay-19f455fe-playable-20260927',theme:'lower-bay',bounds:{min:[-67,-1.5,-12.5],max:[67,7.8,16.5]},palette:{floor:'#4b4d46',wall:'#b8b7a6',accent:'#b49750',sky:'#151c20'},entities,modules,moduleDefinitions:{'hall-pier':pier(5.5,1.1),'concourse-pier':pier(3.9,.9)},spawns,pickups,zones,routes,
  landmarks:[
    {id:'platform',label:'Platform / train',position:[-27,1.7,3.4],lookAt:[16,1.3,0]},
    {id:'concourse-view',label:'Glass concourse',position:[-28,1.7,11],lookAt:[20,1.5,9]},
    {id:'red-upper-view',label:'Red upper room',position:[-38,5.1,1.7],lookAt:[-18,3.4,0]},
    {id:'ramp-view',label:'Ramp to upper rooms',position:[-31.6,1.7,3.5],lookAt:[-31.6,4.4,12]},
    {id:'alcove-view',label:'Armor / advertisement alcove',position:[5,1.7,-9.5],lookAt:[-4,1.4,-11]},
    {id:'ticket-hall',label:'Fare gates and ticket hall',position:[-16,1.7,10.4],lookAt:[10,1.3,13.5]},
    {id:'service-annex',label:'Electrical room entrance',position:[-36.5,1.6,-6.8],lookAt:[-36.5,1.4,-10.5]},
    {id:'fanlight',label:'Upper room fanlight',position:[-34.5,5.1,.8],lookAt:[-39.9,6.2,0]}
  ],
  dynamics:[{id:'station-train',type:'train',entityIds:trainIds,actorIds:['roof-sniper'],axis:'x',min:0,max:44,speed:7,pause:10,phase:0,hazard:true}],
  sources:[{name:'Lower Bay source repository',role:'dimension and topology estimates',url:'https://github.com/constripacity/lower-bay',commit:'19f455fe689823fd7bfafd2d0180ade8cb836d99'},...['tvBSZ8nwL4M','dOicvrsSRFg','-B_oqYj6YBY'].map(id=>({name:'Video '+id,role:'visual reference',url:'https://www.youtube.com/watch?v='+id})),{name:'lower-bay-reconstruction-study-v1.png',role:'AI reconstruction study'},{name:'ivory-tile-albedo.png',role:'AI wall material'},{name:'concrete-albedo.png',role:'AI floor material'},{name:'lower-bay-sign.png',role:'AI station signage'}],
  provenance:{coordinateFrame:'Authored Unity Y-up metres. Source spec gives estimates with historical X mirrors; this reconstruction directly assigns red to -X, blue to +X and glass concourse to +Z.',geometrySource:'Autonomous agent-authored modular plan from footage, public reconstruction specifications and an AI visual study. Not single-image exact geometry recovery.',observed:['single track and two platforms','square tile piers with dark plinths','glass-sided pillared concourse','narrow opposite flank with central advertisement alcove','upper rooms, rising ramps and dead-end catwalks','grey train with bright windows'],inferred:['Metric scale and exact upper return turns','134m total runout bounds, extending source tunnel length to contain moving train','0..44m train offset, 7m/s, 10s pause and reversing return motion; original timing is unverified','Track access ramps and upper returns tuned for traversal; canonical catwalk drop restored','Materials are new AI interpretations, not recovered original textures'],verificationScope:'Static geometry and authoring-pose reachability; separate runtime train and native importer checks. No original-map fidelity certification or multiplayer match proof.'}
};
plan.sources.push({name:'train-steel-albedo.png',role:'AI train surface material generated 2026-09-27'});
plan.sources.push({name:'winter-poster.png',role:'AI travel artwork from video 3 at 00:25'});
plan.provenance.reconciliation=[
  {feature:'upper-shell-closure',status:'construction-repair',decision:'Close the exposed sky aperture above each spawn/catwalk doorway with a 2.4m-clear header, connect the north spawn wall to the gallery partition, and extend the return floor to that partition. Prior floor edges left a 1.08m fall-through strip. Room positions and verified route centre lines remain unchanged.'},
  {feature:'under-gallery-return',status:'traversal-adaptation',decision:'Add a 1.6m-wide, 2.3m-high west service doorway in the source-anchored restroom envelope. The phase2 five-door glass layout and solid room wall trapped 145 reachable samples beneath the red gallery. Hidden doorway location is inferred; an explicit bidirectional route verifies escape without jumping.'},
  {feature:'station-fixtures',status:'source-anchored-inferred-details',source:'reference/LOWER_BAY_CANONICAL_FACTS.md §3 and spec/module_catalog.json',coordinateConversion:'Canonical OBJ X is reflected to Unity X for asymmetric furniture and room anchors.',anchors:{fareGates:{count:12,x:[-11,11],z:12.5},tickets:{count:4,x:[16.5,21],z:15.2},booth:{x:-7,z:13.6},restroom:{x:[-31,-24],z:[11.5,15.5]},serviceRooms:{x:[-36.5,36.5],z:-9.2}},inferred:'Room doors, wall thickness, cabinet/sink details, booth counter height, annex 3m×3.2m footprint and 2.8m ceiling. Source group bounds contain below-ground fixture origins; fixtures are grounded deliberately.'},
  {feature:'pillars',status:'source-dimensions',source:'spec/module_catalog.json column_revision + fixture_revision',hallShaftWidth:1.1,concourseShaftWidth:.9,hallCount:36,concourseCount:22},
  {feature:'divider-doors',status:'source-counts-with-traversal-adaptation',source:'reference/LOWER_BAY_CANONICAL_FACTS.md §3',southDoorIntervalsUnity:flankDoors,glassCount:5,glassInnerCentersUnity:[-12.16,1.52,15.2],inferred:'Outer glass centers ±31.6 and 3.35m headers follow the already validated rising ramps; canonical fractional outer centers and 2.4m headers conflict with this authored upper topology.'},
  {feature:'fanlights',status:'source-identity-inferred-dimensions',source:'reference/LOWER_BAY_CANONICAL_FACTS.md §3; ASTRA_VIDEO_NOTES.md clip 3',inferred:'24-strip semicircle per upper-room end, radius1.4m and base5.75m fitted above the closed door. Exact original fanlight placement/radius is not certified.'},
  {feature:'catwalk-roof-transitions',status:'source-gameplay-restored',source:'reference/LOWER_BAY_CANONICAL_FACTS.md §§3/5',decision:'Remove bidirectional wedges, extend decks to X=+/-18 and restore the committed 0.8m drop. Return uses the track/platform/upper-room loop or a deliberate jump.'},
  {feature:'winter-advertisement',status:'ai-interpretation',source:'Video -B_oqYj6YBY at 00:25',asset:'winter-poster.png',decision:'Original winter tower travel artwork, fitted to two landscape advertisement panels; not recovered source artwork.'}
];
const assets={};const imageReceipt=[];
for(const file of ['ivory-tile-albedo.png','concrete-albedo.png','lower-bay-sign.png','winter-poster.png','train-steel-albedo.png']){const bytes=fs.readFileSync(path.join(base,'generated',file));assets['textures/'+file]='data:image/png;base64,'+bytes.toString('base64');imageReceipt.push({file,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex'),consumer:'Entity material.texture, browser preview, Blender scene and Unity importer'});}
fs.mkdirSync(path.join(base,'output'),{recursive:true});
fs.writeFileSync(path.join(base,'output/lower-bay.plan.json'),JSON.stringify(plan,null,2));
const map=compileScenePlan(plan,{assets});
fs.writeFileSync(path.join(base,'output/lower-bay.strikemap.json'),JSON.stringify(map));
fs.writeFileSync(path.join(base,'output/validation.json'),JSON.stringify(map.analysis.validation,null,2));
fs.writeFileSync(path.join(base,'generated/ASSET-RECEIPT.json'),JSON.stringify(imageReceipt,null,2));
console.log(JSON.stringify({entities:map.entities.length,spawns:map.spawns.length,pickups:map.pickups.length,valid:map.analysis.validation.valid,metrics:map.analysis.validation.metrics,errors:map.analysis.validation.errors},null,2));
if(!map.analysis.validation.valid)process.exitCode=1;
