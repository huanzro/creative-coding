/* ARTISTIC RATIONALE
 * An original floating dance built around breath, gathering light and letting it go.
 * Weight shifts start at the body. Hands finish gestures and fabric follows later.
 */

// Body: shift x/y (scene units), lean, turn and gaze. Feet: travel,
// lift, toe angle and support (1 = take weight, 0 = free to step).
const dance = [
  // Prepare with a downward sweep and let the left toe take weight.
  { at: 0,    body: [  0,  0,  .03, -.04,  .02], left: [ .03, .18,-.16], right: [ .10, .17,-.05], leftFoot: [-8, 7, -.05, .54], rightFoot: [ 8,18, .08,.28] },
  { at: .11, body: [ -9,  5, -.14, -.44,  .10], left: [-.55, .54,-.33], right: [-.30, .28,.14], leftFoot: [-11,4, -.10, .78], rightFoot: [20,55, .18, 0] },
  { at: .19, body: [-11,  3, -.20, -.70,  .17], left: [-.28, .84,-.24], right: [-.42, .67,.17], leftFoot: [-18,4, -.13, .75], rightFoot: [36,70, .24, 0] },
  // Rise through the hip, then let the left hand and wrist finish the reach.
  { at: .29, body: [ -7, -9, -.16, -.82,  .22], left: [ .78, .72, .20], right: [-.34, .51,-.15], leftFoot: [-16,18,-.16, .28], rightFoot: [40,81, .24, 0] },
  { at: .38, body: [  1, -8, -.035,-.18, .06], left: [ .61, .26, .27], right: [ .48, .32,.14], leftFoot: [-38,51,-.17, 0], rightFoot: [23,35, .05,.22] },
  { at: .48, body: [  8,  1,  .13,  .52,-.10], left: [ .05, .04, .02], right: [ .40, .16,-.04], leftFoot: [-43,62,-.15, 0], rightFoot: [18, 5, .04,.70] },
  // Land into a softer bend before answering with the other arm.
  { at: .57, body: [ 11,  5,  .20,  .76,-.16], left: [-.48, .30, .14], right: [-.34, .84,-.26], leftFoot: [-36,69,-.23, 0], rightFoot: [15, 4, .09,.79] },
  { at: .70, body: [  7, -9,  .17,  .84,-.22], left: [-.34, .58,-.10], right: [ .77, .74,.25], leftFoot: [-37,78,-.25, 0], rightFoot: [23,21, .12,.28] },
  { at: .80, body: [ -1, -7,  .06,  .26,-.12], left: [ .28, .22, .20], right: [ .59, .24,.19], leftFoot: [-26,32,-.08,.25], rightFoot: [35,45, .19, 0] },
  { at: .91, body: [ -5,  2, -.09, -.36, .10], left: [ .24, .39,-.05], right: [-.37, .50,-.15], leftFoot: [-15, 6,-.06,.64], rightFoot: [16,46, .18, 0] }
];

const tracks = ['body', 'left', 'right', 'leftFoot', 'rightFoot'];

function tangent(before, after) {
  // Monotone cubic interpolation settles into a held pose without overshoot.
  if (before * after <= 0) return 0;
  return 2 * before * after / (before + after);
}

export function danceAt(phase) {
  const time = phase - Math.floor(phase);
  const n = dance.length;
  let index = n - 1;
  for (let i = 0; i < n - 1; i++) {
    if (time >= dance[i + 1].at) continue;
    index = i;
    break;
  }
  const prev = dance[(index + n - 1) % n];
  const a = dance[index];
  const b = dance[(index + 1) % n];
  const next = dance[(index + 2) % n];
  const span = (b.at - a.at + 1) % 1;
  const previousSpan = (a.at - prev.at + 1) % 1;
  const nextSpan = (next.at - b.at + 1) % 1;
  const t = (time - a.at) / span;
  const t2 = t * t, t3 = t2 * t;
  const result = {};
  for (const track of tracks) {
    result[track] = a[track].map((value, i) => {
      const end = b[track][i];
      const speed = (end - value) / span;
      const m0 = span * tangent((value - prev[track][i]) / previousSpan, speed);
      const m1 = span * tangent(speed, (next[track][i] - end) / nextSpan);
      return value * (2*t3 - 3*t2 + 1) + m0 * (t3 - 2*t2 + t)
        + end * (-2*t3 + 3*t2) + m1 * (t3 - t2);
    });
  }
  return result;
}
