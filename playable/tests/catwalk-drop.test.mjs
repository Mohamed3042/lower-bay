import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createCollisionWorld,createPlayer,stepPlayer,V2_PLAYER} from '../lib/physics.js';

const map=JSON.parse(fs.readFileSync(new URL('../output/lower-bay.strikemap.json',import.meta.url),'utf8'));

test('train roof cannot be walked back onto either committed-drop catwalk',()=>{
  for(const side of [-1,1]){
    const world=createCollisionWorld(map.entities,{profile:V2_PLAYER});
    const player=createPlayer([side*17,2.2,0]);
    for(let i=0;i<180;i++)stepPlayer(player,{x:side},world,1/60);
    assert.ok(player.y<2.7,`side ${side}: walking returned to catwalk at ${JSON.stringify(player)}`);
    assert.ok(Math.abs(player.x)<18,`side ${side}: passed through catwalk lip`);
  }
});

test('catwalk tips preserve the source drop points and open inner ends',()=>{
  assert.ok(!map.entities.some(e=>e.id.endsWith('train-catwalk-ramp')),'the old return wedges erase the source gameplay commitment');
  for(const side of ['red','blue']){
    const deck=map.entities.find(e=>e.id===side+'-catwalk');
    assert.equal(Math.abs(deck.position[0])-deck.size[0]/2,18);
    assert.equal(deck.position[1]+deck.size[1]/2,3);
  }
  assert.equal(map.routes.find(r=>r.id==='roof-sniper-access').direction,'forward');
});
