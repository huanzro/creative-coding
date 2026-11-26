const unit = value => Math.max(0, Math.min(1, value));
export const handSampleInterval = 1000 / 30;

export async function loadHandDetector() {
  const { FilesetResolver, HandLandmarker } = await import('./vendor/vision_bundle.mjs');
  const vendor = new URL('./vendor/', import.meta.url);
  const files = await FilesetResolver.forVisionTasks(new URL('wasm', vendor).href);
  return HandLandmarker.createFromOptions(files, {
    baseOptions: { modelAssetPath: new URL('hand_landmarker.task', vendor).href, delegate: 'CPU' },
    runningMode: 'VIDEO', numHands: 1, minHandDetectionConfidence: .55,
    minHandPresenceConfidence: .55, minTrackingConfidence: .55
  });
}

export class HandFrameClock {
  reset() { this.next = undefined; }

  take(captureTime) {
    if (!Number.isFinite(captureTime)) return false;
    if (this.next === undefined) this.next = captureTime;
    if (captureTime + 2 < this.next) return false;
    // Keep the capture cadence when a single frame is a little early or late.
    this.next = Math.max(this.next + handSampleInterval, captureTime + handSampleInterval * .5);
    return true;
  }
}

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

function hasLandmarks(points, aspect) {
  return points?.length === 21 && Number.isFinite(aspect) && aspect > 0 &&
    points.every(p => p && Number.isFinite(p.x) && Number.isFinite(p.y) && Number.isFinite(p.z));
}

export function indexTip(points, aspect, continuing = false) {
  if (!hasLandmarks(points, aspect)) return null;
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

function penLifted(points, aspect) {
  if (!hasLandmarks(points, aspect)) return false;
  const index = finger(points, 5, aspect);
  const middle = finger(points, 9, aspect);
  return index.reach < .70 || (middle.reach > .92 && middle.hinge > .69 && middle.tip > .56);
}

export class IndexTipTracker {
  reset() {
    this.since = undefined;
    this.lastAt = undefined;
    this.lastTip = null;
    this.interrupted = false;
  }

  update(points, aspect, now) {
    if (now - this.lastAt > 100) this.reset();
    let tip = indexTip(points, aspect, Boolean(this.lastTip));
    if (!tip) {
      if (this.lastTip && !penLifted(points, aspect)) {
        this.interrupted = true;
        return this.lastTip;
      }
      this.reset();
      return null;
    }
    if (this.interrupted && Math.hypot(tip.x - this.lastTip.x, tip.y - this.lastTip.y) > .18) {
      this.reset();
      tip = indexTip(points, aspect);
      if (!tip) return null;
    }
    this.lastAt = now;
    if (this.since === undefined) this.since = now;
    if (now - this.since < 28) return null;
    this.interrupted = false;
    this.lastTip = tip;
    return tip;
  }
}

export function withIndexTip(pose, tip) {
  return tip ? pose + '/' + Math.round(tip.x * 10000) + '/' + Math.round(tip.y * 10000) : pose;
}
