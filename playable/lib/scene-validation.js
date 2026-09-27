import { createCollisionWorld, supportSurfaces, playerIntersects, V2_PLAYER } from './physics.js';

const EPS = 1e-5, HEX = /^#[0-9a-f]{6}$/i;
const KINDS = new Set(['floor', 'wall', 'cover', 'platform', 'ramp', 'decoration']);
const finite = (n) => typeof n === 'number' && Number.isFinite(n);
const vector = (v) => Array.isArray(v) && v.length === 3 && v.every(finite);
const object = (v) => v && typeof v === 'object' && !Array.isArray(v);
const text = (v, max = 255) => typeof v === 'string' && v.length > 0 && v.length <= max;
const round = (v) => Math.round(v * 10000) / 10000;
const validBounds = (b) => object(b) && vector(b.min) && vector(b.max) && b.min.every((v, i) => Math.abs(v) <= 10000 && Math.abs(b.max[i]) <= 10000 && b.max[i] > v && b.max[i] - v <= (i === 1 ? 256 : 2048));
const inBounds = (p, b, margin = 0) => p.every((v, i) => v >= b.min[i] + margin - EPS && v <= b.max[i] - margin + EPS);
const required = (item) => item.required !== false;

function schema(map) {
  const errors = [];
  if (!object(map)) return ['Scene must be an object.'];
  if (map.version !== '2.0' || map.mode !== 'reconstruction') errors.push('Scene requires version 2.0 and reconstruction mode.');
  if (!text(map.id) || !text(map.name, 120) || !text(map.seed, 200) || !text(map.theme, 120)) errors.push('Scene identity fields must be bounded strings.');
  if (!validBounds(map.bounds)) errors.push('Scene bounds must be finite increasing 3D vectors with bounded extents.');
  else if (!finite(map.size) || Math.abs(map.size - Math.max(map.bounds.max[0] - map.bounds.min[0], map.bounds.max[2] - map.bounds.min[2])) > .001) errors.push('Scene size must equal the larger rectangular footprint dimension.');
  if (!object(map.palette) || !['floor', 'wall', 'accent', 'sky'].every((key) => HEX.test(map.palette[key]))) errors.push('Scene palette requires valid floor, wall, accent and sky colours.');
  if (!Array.isArray(map.entities) || map.entities.length > 10_000) errors.push('Scene entities must be an array of at most 10,000 primitives.');
  if (!Array.isArray(map.spawns) || map.spawns.length > 128 || !Array.isArray(map.pickups) || map.pickups.length > 1024) errors.push('Scene requires bounded spawn and pickup arrays.');
  if (map.sources !== undefined && (!Array.isArray(map.sources) || map.sources.length > 64 || !map.sources.every((s) => object(s) && text(s.name) && (s.role === undefined || text(s.role, 120))))) errors.push('Scene sources require bounded names and optional roles.');
  if (map.analysis !== undefined && !object(map.analysis)) errors.push('Scene analysis must be an object.');
  if (map.assets !== undefined) {
    if (!object(map.assets) || Object.keys(map.assets).length > 64) errors.push('Scene assets must be a dictionary of at most 64 embedded images.');
    else {
      let bytes = 0;
      for (const [key, value] of Object.entries(map.assets)) {
        if (!/^textures\/[a-zA-Z0-9_-]+\.(png|jpe?g|webp)$/.test(key) || typeof value !== 'string' || !/^data:image\/(png|jpeg|webp);base64,[a-zA-Z0-9+/]+={0,2}$/.test(value) || value.length > 16_000_000) errors.push(`Invalid embedded texture asset: ${key}.`);
        bytes += typeof value === 'string' ? value.length : 0;
      }
      if (bytes > 64_000_000) errors.push('Scene embedded textures exceed the 64MB encoded limit.');
    }
  }
  for (const [field, maximum] of [['zones', 256], ['routes', 512], ['landmarks', 256], ['dynamics', 128]]) if (map[field] !== undefined && (!Array.isArray(map[field]) || map[field].length > maximum)) errors.push(`${field} must be a bounded array.`);
  if (errors.length) return errors;
  const ids = new Set(), entityIds = new Set(), actorIds = new Set();
  let estimatedBucketReferences = 0;
  function identity(item, label, domain = ids) {
    if (!object(item) || !text(item.id) || domain.has(item.id)) { errors.push(`${label} requires a unique bounded ID.`); return false; }
    domain.add(item.id); return true;
  }
  for (const e of map.entities) {
    if (!identity(e, 'Entity', entityIds)) continue;
    if (!KINDS.has(e.kind) || !vector(e.position) || !vector(e.size) || e.size.some((v) => v <= 0 || v > 2048) || !vector(e.rotation) || e.rotation[0] || e.rotation[2] || Math.abs(e.rotation[1]) > 360000) { errors.push(`Entity ${e.id} has an unsupported shape or invalid transform.`); continue; }
    if (!HEX.test(e.color) || typeof e.collidable !== 'boolean' || (e.walkable !== undefined && typeof e.walkable !== 'boolean') || (e.required !== undefined && typeof e.required !== 'boolean') || (e.roof !== undefined && typeof e.roof !== 'boolean')) errors.push(`Entity ${e.id} has invalid surface metadata.`);
    if ((e.zone !== undefined && !text(e.zone)) || (e.label !== undefined && !text(e.label, 500))) errors.push(`Entity ${e.id} has invalid zone/label data.`);
    const radians = e.rotation[1] * Math.PI / 180, hx = Math.abs(Math.cos(radians)) * e.size[0] / 2 + Math.abs(Math.sin(radians)) * e.size[2] / 2, hz = Math.abs(Math.sin(radians)) * e.size[0] / 2 + Math.abs(Math.cos(radians)) * e.size[2] / 2;
    if (e.collidable) estimatedBucketReferences += (Math.ceil((hx * 2 + 1.02) / 4) + 1) * (Math.ceil((hz * 2 + 1.02) / 4) + 1);
    if (!inBounds([e.position[0] - hx, e.position[1] - e.size[1] / 2, e.position[2] - hz], map.bounds) || !inBounds([e.position[0] + hx, e.position[1] + e.size[1] / 2, e.position[2] + hz], map.bounds)) errors.push(`Entity ${e.id} extends outside scene bounds.`);
    if (e.material !== undefined) {
      const m = e.material;
      if (!object(m)) { errors.push(`Entity ${e.id} material must be an object.`); continue; }
      for (const key of ['opacity', 'roughness', 'metalness', 'emissiveIntensity']) if (m[key] !== undefined && (!finite(m[key]) || m[key] < 0 || m[key] > (key === 'emissiveIntensity' ? 5 : 1))) errors.push(`Entity ${e.id} material ${key} is invalid.`);
      if (m.emissive !== undefined && !HEX.test(m.emissive)) errors.push(`Entity ${e.id} emissive colour is invalid.`);
      if (m.texture !== undefined && (!text(m.texture) || !Object.hasOwn(map.assets || {}, m.texture))) errors.push(`Entity ${e.id} references a missing texture asset.`);
      if (m.uvScale !== undefined && (!Array.isArray(m.uvScale) || m.uvScale.length !== 2 || !m.uvScale.every((n) => finite(n) && n > 0 && n <= 10000))) errors.push(`Entity ${e.id} UV scale is invalid.`);
    }
  }
  if (estimatedBucketReferences > 1_000_000) errors.push('Scene collision span exceeds the bounded interactive validation budget.');
  for (const [kind, actors] of [['Spawn', map.spawns], ['Pickup', map.pickups]]) for (const actor of actors) {
    if (!identity(actor, kind, actorIds)) continue;
    if (!vector(actor.position) || !inBounds(actor.position, map.bounds) || (actor.required !== undefined && typeof actor.required !== 'boolean')) errors.push(`${kind} ${actor.id} has an invalid absolute position.`);
    if (kind === 'Spawn' && (!['red', 'blue', 'neutral'].includes(actor.team) || !finite(actor.yaw))) errors.push(`Spawn ${actor.id} requires a valid team and yaw.`);
    if (kind === 'Pickup' && !['health', 'armor', 'ammo', 'sniper', 'heavy-armor'].includes(actor.kind)) errors.push(`Pickup ${actor.id} has an unsupported kind.`);
  }
  const gameplayActorIds = new Set(actorIds);
  for (const zone of map.zones || []) if (identity(zone, 'Zone', actorIds) && (!text(zone.name) || !['observed', 'inferred'].includes(zone.confidence) || (zone.floorY !== undefined && !finite(zone.floorY)) || (zone.bounds !== undefined && !validBounds(zone.bounds)))) errors.push(`Zone ${zone.id} has invalid metadata.`);
  let routePoints = 0;
  for (const route of map.routes || []) {
    if (!identity(route, 'Route')) continue;
    if (!text(route.from) || !text(route.to) || !['both', 'forward'].includes(route.direction) || !Array.isArray(route.points) || route.points.length < 2 || route.points.length > 4096 || !route.points.every(vector) || (route.evidence !== undefined && typeof route.evidence !== 'string')) errors.push(`Route ${route.id} has invalid endpoints or points.`);
    routePoints += route.points?.length || 0;
  }
  if (routePoints > 50_000) errors.push('Scene has too many route sample points.');
  for (const mark of map.landmarks || []) if (identity(mark, 'Landmark') && (!vector(mark.position) || !vector(mark.lookAt) || !text(mark.label, 500))) errors.push(`Landmark ${mark.id} is malformed.`);
  const moving = new Set(), movingActors = new Set();
  for (const dynamic of map.dynamics || []) {
    if (!identity(dynamic, 'Dynamic')) continue;
    if (dynamic.type !== 'train' || !['x', 'z'].includes(dynamic.axis) || !Array.isArray(dynamic.entityIds) || !dynamic.entityIds.length || dynamic.entityIds.some((id) => !entityIds.has(id) || moving.has(id)) || new Set(dynamic.entityIds).size !== dynamic.entityIds.length || !finite(dynamic.min) || !finite(dynamic.max) || Math.abs(dynamic.min) > 2048 || Math.abs(dynamic.max) > 2048 || dynamic.min >= dynamic.max || !finite(dynamic.speed) || dynamic.speed < .01 || dynamic.speed > 100 || !finite(dynamic.pause) || dynamic.pause < 0 || dynamic.pause > 600 || !finite(dynamic.phase) || typeof dynamic.hazard !== 'boolean') errors.push(`Dynamic ${dynamic.id} has invalid motion parameters or entity membership.`);
    else for (const id of dynamic.entityIds) moving.add(id);
    if (dynamic.actorIds !== undefined) {
      if (!Array.isArray(dynamic.actorIds) || dynamic.actorIds.length > 1024 || new Set(dynamic.actorIds).size !== dynamic.actorIds.length || dynamic.actorIds.some((id) => !gameplayActorIds.has(id) || movingActors.has(id))) errors.push(`Dynamic ${dynamic.id} actorIds must reference unique unattached gameplay actors.`);
      else for (const id of dynamic.actorIds) movingActors.add(id);
    }
  }
  return errors;
}

/** Spatial buckets are inflated by the capsule radius, so one bucket query
 * includes every collider which can intersect the player at a sample point. */
function sampler(map) {
  const world = createCollisionWorld(map.entities, { profile: V2_PLAYER }), buckets = new Map(), bucketSize = 4;
  for (const c of world) {
    const hx = Math.abs(c.cos) * c.hx + Math.abs(c.sin) * c.hz + V2_PLAYER.radius + .01;
    const hz = Math.abs(c.sin) * c.hx + Math.abs(c.cos) * c.hz + V2_PLAYER.radius + .01;
    for (let z = Math.floor((c.z - hz) / bucketSize); z <= Math.floor((c.z + hz) / bucketSize); z++) for (let x = Math.floor((c.x - hx) / bucketSize); x <= Math.floor((c.x + hx) / bucketSize); x++) {
      const key = `${x},${z}`; if (!buckets.has(key)) buckets.set(key, []); buckets.get(key).push(c);
    }
  }
  const nearby = (x, z) => buckets.get(`${Math.floor(x / bucketSize)},${Math.floor(z / bucketSize)}`) || [];
  const clear = (x, y, z) => !playerIntersects(nearby(x, z), { x, y, z }, V2_PLAYER);
  const cache = new Map();
  function surfaces(x, z) {
    const key = `${round(x)},${round(z)}`;
    if (cache.has(key)) return cache.get(key);
    const local = nearby(x, z), result = [];
    for (const s of supportSurfaces(local, x, z, { walkableOnly: true, maxSlope: V2_PLAYER.maxSlope, profile: V2_PLAYER })) {
      if (!clear(x, s.height, z)) continue;
      const existing = result.find((t) => Math.abs(t.y - s.height) < .001);
      if (existing) existing.supportIds.push(s.id);
      else result.push({ x, y: s.height, z, supportIds: [s.id] });
    }
    result.sort((a, b) => a.y - b.y);
    if (cache.size < 200_000) cache.set(key, result);
    return result;
  }
  function walk(a, b) {
    const distance = Math.hypot(a.x - b.x, a.z - b.z), steps = Math.max(1, Math.ceil(distance / .125));
    let y = a.y;
    for (let i = 1; i <= steps; i++) {
      const t = i / steps, x = a.x + (b.x - a.x) * t, z = a.z + (b.z - a.z) * t, expected = a.y + (b.y - a.y) * t;
      const options = surfaces(x, z).filter((s) => Math.abs(s.y - y) <= V2_PLAYER.stepHeight + .001).sort((u, v) => Math.abs(u.y - expected) - Math.abs(v.y - expected));
      if (!options.length) return false;
      y = options[0].y;
    }
    return Math.abs(y - b.y) < .04;
  }
  function drop(a, b) {
    const fall = a.y - b.y, distance = Math.hypot(a.x - b.x, a.z - b.z);
    if (fall <= V2_PLAYER.stepHeight || fall > 8 || distance > V2_PLAYER.walkSpeed * Math.sqrt(2 * fall / V2_PLAYER.gravity) + V2_PLAYER.radius) return false;
    // A conservative clear corridor: approach at upper height, then descend
    // beyond the ledge. This proves directed geometric access, not fall damage.
    const steps = Math.max(1, Math.ceil(distance / .125));
    for (let i = 1; i <= steps; i++) if (!clear(a.x + (b.x - a.x) * i / steps, a.y, a.z + (b.z - a.z) * i / steps)) return false;
    const verticalSteps = Math.ceil(fall / .125);
    for (let i = 1; i <= verticalSteps; i++) if (!clear(b.x, a.y - fall * i / verticalSteps, b.z)) return false;
    return surfaces(b.x, b.z).some((s) => Math.abs(s.y - b.y) < .04);
  }
  const explain = (x, y, z) => {
    const local = nearby(x, z);
    return {
      supportHeights: [...new Set(supportSurfaces(local, x, z, { walkableOnly: true, maxSlope: V2_PLAYER.maxSlope, profile: V2_PLAYER }).map((s) => round(s.height)))].slice(0, 8),
      blockers: local.filter((c) => playerIntersects([c], { x, y, z }, V2_PLAYER)).map((c) => c.id).slice(0, 8),
    };
  };
  return { world, nearby, surfaces, clear, walk, drop, explain, bucketCount: buckets.size };
}

function buildGraph(map, samples) {
  const [x0, , z0] = map.bounds.min, width = map.bounds.max[0] - x0, depth = map.bounds.max[2] - z0;
  const target = Math.max(.5, Math.max(width, depth) / 256), nx = Math.ceil(width / target), nz = Math.ceil(depth / target), dx = width / nx, dz = depth / nz;
  const nodes = [], cells = Array.from({ length: nx * nz }, () => []), bySupport = new Map();
  for (let z = 0; z < nz; z++) for (let x = 0; x < nx; x++) {
    const cell = z * nx + x;
    for (const point of samples.surfaces(x0 + (x + .5) * dx, z0 + (z + .5) * dz)) {
      if (nodes.length >= 250_000) throw new Error('Scene navigation exceeds 250,000 sampled support nodes.');
      const id = nodes.length, node = { ...point, id, cell, links: [], reverse: [] }; nodes.push(node); cells[cell].push(id);
      for (const support of point.supportIds) { if (!bySupport.has(support)) bySupport.set(support, []); bySupport.get(support).push(id); }
    }
  }
  let walkingLinks = 0, directedDropLinks = 0;
  function link(a, b, isDrop = false) { if (a.links.includes(b.id)) return; a.links.push(b.id); b.reverse.push(a.id); if (isDrop) directedDropLinks++; else walkingLinks++; }
  for (const a of nodes) {
    const x = a.cell % nx, z = Math.floor(a.cell / nx);
    for (const [ox, oz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const tx = x + ox, tz = z + oz;
      if (tx < 0 || tz < 0 || tx >= nx || tz >= nz) continue;
      for (const id of cells[tz * nx + tx]) {
        const b = nodes[id];
        if (Math.abs(a.y - b.y) <= target * V2_PLAYER.maxSlope + V2_PLAYER.stepHeight + .02 && samples.walk(a, b)) link(a, b);
        else if (a.y > b.y + V2_PLAYER.stepHeight && samples.drop(a, b)) link(a, b, true);
      }
      const fx = x + ox * 2, fz = z + oz * 2;
      if (fx < 0 || fz < 0 || fx >= nx || fz >= nz) continue;
      for (const id of cells[fz * nx + fx]) { const b = nodes[id]; if (a.y > b.y + V2_PLAYER.stepHeight && samples.drop(a, b)) link(a, b, true); }
    }
  }
  function anchor(position) {
    if (!inBounds(position, map.bounds)) return { error: 'is outside scene bounds.' };
    const [x, y, z] = position;
    const support = samples.surfaces(x, z).find((s) => Math.abs(s.y - y) < .055);
    if (!support) {
      const detail = samples.explain(x, y, z);
      return { error: `has no clear support at surface Y=${round(y)} (embedded/blocked or wrong height); supports=[${detail.supportHeights.join(',')}], blockers=[${detail.blockers.join(',')}].` };
    }
    const gx = Math.floor((x - x0) / dx), gz = Math.floor((z - z0) / dz), candidates = [];
    for (let az = Math.max(0, gz - 2); az <= Math.min(nz - 1, gz + 2); az++) for (let ax = Math.max(0, gx - 2); ax <= Math.min(nx - 1, gx + 2); ax++) for (const id of cells[az * nx + ax]) {
      const n = nodes[id], distance = Math.hypot(n.x - x, n.z - z);
      if (distance < target * 2 && Math.abs(n.y - y) <= distance * V2_PLAYER.maxSlope + .4) candidates.push({ node: n, distance });
    }
    candidates.sort((a, b) => a.distance - b.distance);
    const result = candidates.find(({ node }) => samples.walk(support, node) && samples.walk(node, support));
    return result ? { node: result.node.id, point: support } : { error: 'has support but cannot connect to the sampled movement graph.' };
  }
  return { nodes, cells, nx, nz, dx, dz, target, walkingLinks, directedDropLinks, bySupport, anchor };
}

function reachable(nodes, start, reverse = false) {
  const seen = new Uint8Array(nodes.length);
  if (start === undefined || start < 0) return seen;
  const queue = new Int32Array(nodes.length); queue[0] = start; seen[start] = 1;
  for (let head = 0, tail = 1; head < tail; head++) for (const next of nodes[queue[head]][reverse ? 'reverse' : 'links']) if (!seen[next]) { seen[next] = 1; queue[tail++] = next; }
  return seen;
}

/** Layered geometry validator. Static pose, standing capsule and explicit drops;
 * combat, crouching, jump tricks and dynamic-train temporal safety are separate. */
export function validateSceneMap(map) {
  const errors = schema(map), warnings = [], routeResults = [];
  const diagnostics = { unreachableSamples: [], noReturnSamples: [] };
  const metrics = { entityCount: Array.isArray(map?.entities) ? map.entities.length : 0, spawnCount: Array.isArray(map?.spawns) ? map.spawns.length : 0, pickupCount: Array.isArray(map?.pickups) ? map.pickups.length : 0, reachablePercent: 0, coveragePercent: 0, clearanceRadius: V2_PLAYER.radius, standingHeight: V2_PLAYER.height, stepHeight: V2_PLAYER.stepHeight, walkableLevels: 0, sampledSupportNodes: 0 };
  const finish = (schemaValid) => ({ valid: schemaValid && !errors.length, schemaValid, errors: errors.slice(0, 100), warnings, metrics, routeResults, diagnostics, scope: 'Authored static pose; layered standing support, 0.125m capsule sweep samples and directed geometric drops. No combat, crouch, jump or moving-train timing certification.' });
  if (errors.length) return finish(false);
  const samples = sampler(map);
  let graph;
  try { graph = buildGraph(map, samples); } catch (error) { errors.push(error.message); return finish(true); }
  const { nodes } = graph, actors = new Map(), actorNodes = new Map(), spawnNodes = [];
  Object.assign(metrics, {
    sampledSupportNodes: nodes.length, walkableCells: graph.cells.filter((c) => c.length).length,
    gridCellMetres: round(Math.max(graph.dx, graph.dz)), sweepSampleMetres: .125,
    coveragePercent: round((1 - graph.cells.filter((c) => c.length).length / graph.cells.length) * 100),
    walkingLinks: graph.walkingLinks, directedDropLinks: graph.directedDropLinks, spatialBuckets: samples.bucketCount,
    walkableLevels: new Set(map.entities.filter((e) => e.collidable && e.kind !== 'ramp' && (e.walkable ?? ['floor', 'platform'].includes(e.kind))).map((e) => round(e.position[1] + e.size[1] / 2))).size,
  });
  if (!nodes.length) errors.push('Scene has no clear walkable support nodes.');
  for (const [role, list, offset] of [['Spawn', map.spawns, 1.1], ['Pickup', map.pickups, .8]]) for (const actor of list) {
    actors.set(actor.id, actor);
    const result = graph.anchor([actor.position[0], actor.position[1] - offset, actor.position[2]]);
    if (result.error) { (required(actor) ? errors : warnings).push(`${role} ${actor.id} ${result.error}`); continue; }
    actorNodes.set(actor.id, result.node);
    if (role === 'Spawn' && required(actor)) spawnNodes.push(result.node);
  }
  const requiredSpawns = map.spawns.filter(required);
  if (requiredSpawns.length < 2) errors.push('At least two required scene spawns are needed.');
  const main = spawnNodes[0], fromSpawn = reachable(nodes, main), toSpawn = reachable(nodes, main, true);
  metrics.reachablePercent = round(nodes.length ? fromSpawn.reduce((a, b) => a + b, 0) / nodes.length * 100 : 0);
  metrics.returnReachablePercent = round(nodes.length ? toSpawn.reduce((a, b) => a + b, 0) / nodes.length * 100 : 0);
  // Keep actionable positions without serializing the full navigation graph.
  for (const [seen, output] of [[fromSpawn, diagnostics.unreachableSamples], [toSpawn, diagnostics.noReturnSamples]]) {
    for (const node of nodes) if (!seen[node.id] && output.length < 16) output.push({ position: [node.x, node.y, node.z].map(round), supportIds: node.supportIds });
  }
  for (const spawn of requiredSpawns) {
    const node = actorNodes.get(spawn.id);
    if (node !== undefined && (!fromSpawn[node] || !toSpawn[node])) errors.push(`Required spawn ${spawn.id} is not mutually reachable from the other spawns.`);
  }
  for (const pickup of map.pickups.filter(required)) {
    const node = actorNodes.get(pickup.id);
    if (node !== undefined && !fromSpawn[node]) errors.push(`Required pickup ${pickup.id} is unreachable from the spawns.`);
    else if (node !== undefined && !toSpawn[node]) warnings.push(`Pickup ${pickup.id} is reachable by a directed descent but has no measured return route.`);
  }
  for (const entity of map.entities.filter((e) => e.collidable && required(e) && (e.walkable ?? ['floor', 'platform', 'ramp'].includes(e.kind)))) {
    const supportNodes = graph.bySupport.get(entity.id) || [];
    if (!supportNodes.length) errors.push(`Required walkable surface ${entity.id} has no clear sampled support.`);
    else if (!supportNodes.some((id) => fromSpawn[id])) errors.push(`Required walkable surface ${entity.id} is disconnected or unreachable from the spawns.`);
  }
  const zones = new Map((map.zones || []).map((z) => [z.id, z]));
  for (const route of map.routes || []) {
    const issues = [], anchors = route.points.map((p, i) => {
      const result = graph.anchor(p);
      if (result.error) issues.push(`Route ${route.id} point ${i + 1} ${result.error}`);
      return result;
    });
    for (const [end, id, index] of [['from', route.from, 0], ['to', route.to, anchors.length - 1]]) {
      if (!actors.has(id) && !zones.has(id)) { issues.push(`Route ${route.id} ${end} endpoint ${id} does not exist.`); continue; }
      if (actors.has(id) && !actorNodes.has(id)) issues.push(`Route ${route.id} endpoint ${id} lacks accessible support.`);
      const zone = zones.get(id), point = route.points[index];
      if (zone?.bounds && !inBounds(point, zone.bounds)) issues.push(`Route ${route.id} endpoint lies outside zone ${id}.`);
    }
    for (let index = 0; index + 1 < anchors.length; index++) {
      if (!anchors[index].point || !anchors[index + 1].point) continue;
      const a = anchors[index].point, b = anchors[index + 1].point;
      if (!samples.walk(a, b) && !samples.drop(a, b)) issues.push(`Route ${route.id} segment ${index + 1} is blocked or has no supported traversal.`);
      if (route.direction === 'both' && !samples.walk(b, a) && !samples.drop(b, a)) issues.push(`Route ${route.id} segment ${index + 1} has no reverse traversal.`);
    }
    for (const anchor of anchors) if (anchor.node !== undefined && !fromSpawn[anchor.node]) issues.push(`Route ${route.id} contains a required node unreachable from the spawns.`);
    if (actors.has(route.from) && actorNodes.has(route.from) && anchors[0].node !== undefined) {
      const reached = reachable(nodes, actorNodes.get(route.from));
      if (!reached[anchors[0].node]) issues.push(`Route ${route.id} cannot connect its source actor to its first point.`);
      if (route.direction === 'both' && !reachable(nodes, anchors[0].node)[actorNodes.get(route.from)]) issues.push(`Route ${route.id} has no return path to its source actor.`);
    }
    if (actors.has(route.to) && actorNodes.has(route.to) && anchors.at(-1).node !== undefined) {
      const reached = reachable(nodes, anchors.at(-1).node);
      if (!reached[actorNodes.get(route.to)]) issues.push(`Route ${route.id} cannot connect its last point to its destination actor.`);
      if (route.direction === 'both' && !reachable(nodes, actorNodes.get(route.to))[anchors.at(-1).node]) issues.push(`Route ${route.id} destination actor has no return path to its last point.`);
    }
    routeResults.push({ id: route.id, valid: !issues.length, points: route.points.length, direction: route.direction, errors: issues });
    errors.push(...issues);
  }
  if ((map.dynamics || []).length) warnings.push('Moving train validation covers authored static positions only; temporal collision, hazards and reset behavior require separate runtime tests.');
  warnings.push('Geometry sampling is not combat or multiplayer playtesting; crouching and jump-only links are not inferred.');
  return finish(true);
}
