const unit = value => Math.max(0, Math.min(1, value));

function finger(points, start, aspect) {
  const offsets = [0, 1, 2].map(i => {
    const a = points[start + i], b = points[start + i + 1];
    return [(b.x - a.x) * aspect, b.y - a.y, (b.z - a.z) * aspect];
  });
  const [a, b, c] = offsets;
  const lengths = offsets.map(v => Math.hypot(...v));
  const cosine = (p, q, scale) => scale > .000001
    ? p.reduce((sum, value, i) => sum + value * q[i], 0) / scale : -1;
  return {
    hinge: cosine(a, b, lengths[0] * lengths[1]),
    tip: cosine(b, c, lengths[1] * lengths[2]),
    reach: Math.hypot(...a.map((value, i) => value + b[i] + c[i])) / Math.max(.0001, lengths[0] + lengths[1] + lengths[2])
  };
}

export function indexTip(points, aspect, continuing = false) {
  if (!points || points.length !== 21 || !Number.isFinite(aspect) || aspect <= 0 ||
      points.some(p => !Number.isFinite(p.x) || !Number.isFinite(p.y) || !Number.isFinite(p.z))) return null;
  const index = finger(points, 5, aspect);
  if (index.hinge < (continuing ? .48 : .61) || index.tip < (continuing ? .40 : .51) ||
      index.reach < (continuing ? .82 : .87)) return null;

  const curled = start => {
    const shape = finger(points, start, aspect);
    return shape.reach < (continuing ? .90 : .86) || shape.hinge < (continuing ? .52 : .42);
  };
  if (!curled(9) || (!curled(13) && !curled(17))) return null;
  // A second fully extended finger is a peace sign even if the remaining digits curl.
  if (!curled(13) && finger(points, 13, aspect).reach > .94 ||
      !curled(17) && finger(points, 17, aspect).reach > .94) return null;
  const { x, y } = points[8];
  if (x < -.04 || x > 1.04 || y < -.04 || y > 1.04) return null;
  return { x: unit(1 - x), y: unit(y) };
}

export class IndexTipTracker {
  reset() { this.since = undefined; this.active = false; }

  update(points, aspect, now) {
    const tip = indexTip(points, aspect, this.active);
    if (!tip) {
      this.reset();
      return null;
    }
    if (this.since === undefined) this.since = now;
    this.active = this.active || now - this.since >= 45;
    return this.active ? tip : null;
  }
}

export function withIndexTip(pose, tip) {
  return tip ? pose + '/' + Math.round(tip.x * 10000) + '/' + Math.round(tip.y * 10000) : pose;
}
