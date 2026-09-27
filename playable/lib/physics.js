/** Small deterministic kinematic controller. Player y is the height of their feet. */
export const PLAYER = Object.freeze({ radius: 0.32, height: 1.72, eyeHeight: 1.55, stepHeight: 0.32, walkSpeed: 5, runSpeed: 8, jumpSpeed: 6, gravity: 18, maxFrame: 0.1 });
export const V2_PLAYER = Object.freeze({ ...PLAYER, radius: 0.5, height: 2, eyeHeight: 1.8, stepHeight: 0.4, maxSlope: 1 });
const EPS = 1e-5;

export function createPlayer(position = [0, 0, 0]) {
  return { x: position[0], y: position[1], z: position[2], vy: 0, grounded: false };
}

export function createCollisionWorld(entities = [], { profile = PLAYER } = {}) {
  const world = entities.filter(e => e.collidable !== false).map(e => {
    const yaw = (e.rotation?.[1] || 0) * Math.PI / 180;
    return { id: e.id, kind: e.kind, walkable: e.walkable ?? ['floor', 'platform', 'ramp'].includes(e.kind), x: e.position[0], y: e.position[1], z: e.position[2], baseX: e.position[0], baseZ: e.position[2],
      hx: e.size[0] / 2, hy: e.size[1] / 2, hz: e.size[2] / 2, cos: Math.cos(yaw), sin: Math.sin(yaw) };
  });
  Object.defineProperty(world, 'profile', { value: profile, enumerable: false });
  return world;
}

function localPoint(c, x, z) {
  const dx = x - c.x, dz = z - c.z;
  return [c.cos * dx - c.sin * dz, c.sin * dx + c.cos * dz];
}

function intersectsFootprint(c, x, z, radius = PLAYER.radius) {
  const [lx, lz] = localPoint(c, x, z);
  const dx = Math.max(Math.abs(lx) - c.hx, 0), dz = Math.max(Math.abs(lz) - c.hz, 0);
  return dx * dx + dz * dz < radius * radius - EPS;
}

export function colliderSurfaceAt(c, x, z) {
  if (c.kind !== 'ramp') return c.y + c.hy;
  const [, lz] = localPoint(c, x, z);
  return c.y - c.hy + Math.max(0, Math.min(1, (lz + c.hz) / (2 * c.hz))) * 2 * c.hy;
}

function supports(c, x, z, radius = PLAYER.radius) {
  if (c.kind !== 'ramp') return intersectsFootprint(c, x, z, radius);
  const [lx, lz] = localPoint(c, x, z);
  return Math.abs(lx) <= c.hx + EPS && Math.abs(lz) <= c.hz + EPS;
}

/** Highest support in a height interval, or null where there is no walkable surface. */
export function supportSurfaces(world, x, z, { walkableOnly = false, maxSlope = Infinity, profile = world.profile || PLAYER } = {}) {
  return world.filter(c => (!walkableOnly || c.walkable) && (c.kind !== 'ramp' || c.hy / c.hz <= maxSlope + EPS) && supports(c, x, z, profile.radius))
    .map(collider => ({ id: collider.id, height: colliderSurfaceAt(collider, x, z), collider }));
}

export function groundHeight(world, x, z, maximum = Infinity, minimum = -Infinity, profile = world.profile || PLAYER) {
  let height = null;
  for (const c of world) {
    if (!supports(c, x, z, profile.radius)) continue;
    const top = colliderSurfaceAt(c, x, z);
    if (top <= maximum + EPS && top >= minimum - EPS && (height === null || top > height)) height = top;
  }
  return height;
}

function obstructed(world, x, y, z, profile = world.profile || PLAYER) {
  return world.some(c => intersectsFootprint(c, x, z, profile.radius) && y + profile.height > c.y - c.hy + EPS && y < colliderSurfaceAt(c, x, z) - EPS);
}

export function playerIntersects(world, player, profile = world.profile || PLAYER) { return obstructed(world, player.x, player.y, player.z, profile); }

/** Move real colliders in small sweeps; carry riders and resolve a moving train's side contact. */
export function moveColliderGroup(world, colliders, axis, previousOffset, nextOffset, player = null, { hazard = false, profile = world.profile || PLAYER } = {}) {
  const delta = nextOffset - previousOffset;
  const slices = Math.max(1, Math.ceil(Math.abs(delta) / (profile.radius * 0.35)));
  const stride = delta / slices;
  let result = { reset: false, reason: null, carried: false, pushed: false };
  for (let slice = 1; slice <= slices; slice++) {
    const standingSurface = player ? groundHeight(world, player.x, player.z, player.y + .045, player.y - .045, profile) : null;
    const rider = player && !result.reset && player.vy <= 0 && supportSurfaces(colliders, player.x, player.z, { profile }).some(s => Math.abs(s.height - player.y) < 0.045 && (standingSurface === null || standingSurface <= s.height + EPS));
    const offset = previousOffset + stride * slice;
    for (const c of colliders) c[axis] = (axis === 'x' ? c.baseX : c.baseZ) + offset;
    if (!player || result.reset || !delta) continue;
    if (rider) { player[axis] += stride; result.carried = true; }
    else if (playerIntersects(colliders, player, profile)) {
      if (hazard) { result.reset = true; result.reason = 'hazard'; continue; }
      player[axis] += stride; result.pushed = true;
    }
    if ((rider || result.pushed) && playerIntersects(world, player, profile)) {
      const step = rider ? supportSurfaces(world, player.x, player.z, { profile, maxSlope: profile.maxSlope ?? Infinity })
        .filter(s => !colliders.includes(s.collider) && s.height >= player.y - EPS && s.height <= player.y + profile.stepHeight + EPS)
        .sort((a, b) => b.height - a.height).find(s => !obstructed(world, player.x, s.height, player.z, profile)) : null;
      if (step) { player.y = step.height; result.disembarked = step.id; }
      else { result.reset = true; result.reason = 'trapped'; }
    }
  }
  return result;
}

export function isWalkable(world, x, z, y = 0, profile = world.profile || PLAYER) {
  const ground = groundHeight(world, x, z, y + profile.stepHeight, y - profile.stepHeight, profile);
  return ground !== null && !obstructed(world, x, ground, z, profile);
}

/** Mutates and returns player. input.x/z are world-space directions, not displacement. */
export function stepPlayer(player, input, world, deltaSeconds, profile = world.profile || PLAYER) {
  const dt = Math.max(0, Math.min(profile.maxFrame, Number.isFinite(deltaSeconds) ? deltaSeconds : 0));
  if (!dt) return player;
  let dx = Number.isFinite(input.x) ? input.x : 0, dz = Number.isFinite(input.z) ? input.z : 0;
  const length = Math.hypot(dx, dz);
  if (length > 1) { dx /= length; dz /= length; }
  const speed = input.run ? profile.runSpeed : profile.walkSpeed;
  // Short substeps prevent a capsule crossing even a very thin wall on a delayed frame.
  const steps = Math.max(1, Math.ceil(dt / (1 / 120)), Math.ceil(speed * dt / (profile.radius / 3)));
  const tick = dt / steps;
  let jump = Boolean(input.jump);
  for (let index = 0; index < steps; index++) {
    const initialGround = groundHeight(world, player.x, player.z, player.y + 0.035, player.y - 0.035, profile);
    if (initialGround !== null && player.vy <= 0) { player.y = initialGround; player.grounded = true; player.vy = 0; }
    else player.grounded = false;
    if (jump && player.grounded) { player.vy = profile.jumpSpeed; player.grounded = false; }
    jump = false;
    const wasGrounded = player.grounded;
    for (const [axis, movement] of [['x', dx * speed * tick], ['z', dz * speed * tick]]) {
      if (!movement) continue;
      const x = axis === 'x' ? player.x + movement : player.x;
      const z = axis === 'z' ? player.z + movement : player.z;
      const eligible = wasGrounded ? supportSurfaces(world, x, z, { profile, maxSlope: profile.maxSlope ?? Infinity }).filter(s => s.height <= player.y + profile.stepHeight + EPS && s.height >= player.y - profile.stepHeight - EPS) : [];
      const support = eligible.length ? Math.max(...eligible.map(s => s.height)) : null;
      const foot = support !== null ? support : player.y;
      if (!obstructed(world, x, foot, z, profile)) { player.x = x; player.z = z; player.y = foot; }
    }
    player.vy -= profile.gravity * tick;
    let nextY = player.y + player.vy * tick;
    if (player.vy <= 0) {
      const floor = groundHeight(world, player.x, player.z, player.y + EPS, nextY, profile);
      if (floor !== null) { nextY = floor; player.vy = 0; player.grounded = true; }
      else player.grounded = false;
    } else {
      for (const c of world) {
        const bottom = c.y - c.hy;
        if (intersectsFootprint(c, player.x, player.z, profile.radius) && player.y + profile.height <= bottom + EPS && nextY + profile.height >= bottom) {
          nextY = Math.min(nextY, bottom - profile.height); player.vy = 0;
        }
      }
    }
    player.y = nextY;
  }
  return player;
}
