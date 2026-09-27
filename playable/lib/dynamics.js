/** Shared browser / Blender / Unity transport contract: phase is seconds, offsets are metres. */
export function sampleTrainMotion(dynamic, elapsedSeconds) {
  const min = Number(dynamic.min) || 0, max = Number(dynamic.max) || 0;
  const speed = Math.max(0, Number(dynamic.speed) || 0), pause = Math.max(0, Number(dynamic.pause) || 0);
  const span = max - min;
  if (span <= 0 || speed === 0) return { offset: min, velocity: 0, period: 0, stage: 'stationary' };
  const travel = span / speed, period = 2 * (travel + pause);
  const raw = (Number.isFinite(elapsedSeconds) ? elapsedSeconds : 0) + (Number(dynamic.phase) || 0);
  const t = ((raw % period) + period) % period;
  if (t < pause) return { offset: min, velocity: 0, period, stage: 'minimum-pause' };
  if (t < pause + travel) return { offset: min + (t - pause) * speed, velocity: speed, period, stage: 'outbound' };
  if (t < 2 * pause + travel) return { offset: max, velocity: 0, period, stage: 'maximum-pause' };
  return { offset: max - (t - (2 * pause + travel)) * speed, velocity: -speed, period, stage: 'inbound' };
}

export function trainOffset(dynamic, elapsedSeconds) { return sampleTrainMotion(dynamic, elapsedSeconds).offset; }
