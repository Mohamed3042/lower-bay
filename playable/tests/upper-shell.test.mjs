import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createCollisionWorld,createPlayer,stepPlayer,V2_PLAYER} from '../lib/physics.js';
const map=JSON.parse(fs.readFileSync(new URL('../output/lower-bay.strikemap.json',import.meta.url),'utf8'));

test('upper gallery floor reaches its inner partition on both ends',()=>{
  for(const side of [-1,1]){
    const world=createCollisionWorld(map.entities,{profile:V2_PLAYER});
    const player=createPlayer([side*34.65,3.4,8]);
    for(let frame=0;frame<120;frame++)stepPlayer(player,{},world,1/60);
    assert.ok(player.y>=3.39,`Uncovered gallery edge dropped player: ${JSON.stringify(player)}`);
  }
});

test('upper room front has a header above its walkable catwalk entrance',()=>{
  for(const side of [-1,1]){
    const p=[side*32,6.6,0];
    assert.ok(map.entities.some(e=>e.collidable&&e.rotation.every(v=>v===0)&&p.every((v,i)=>Math.abs(v-e.position[i])<=e.size[i]/2)),`Open exterior aperture at ${p}`);
  }
});

test('both rising ramp volumes have an overhead roof',()=>{
  for(const side of [-1,1]){
    const p=[side*31.6,8];
    assert.ok(map.entities.some(e=>e.roof&&e.position[1]-e.size[1]/2>5.4&&Math.abs(p[0]-e.position[0])<=e.size[0]/2&&Math.abs(p[1]-e.position[2])<=e.size[2]/2),'Ramp opens to exterior above '+p);
  }
});
