/* ARTISTIC RATIONALE
 * The woodland fairy moves like a person suspended in air. Each limb has
 * separate joints and the light cloth responds after her change in direction.
 * A shared transparent sprite keeps the projection and game view in sync.
 */
import {danceAt} from './spirit-dance.js';

const portrait = new Image();
portrait.src = new URL('./assets/forest-sylph-long-dress.png', import.meta.url).href;

const ART_WIDTH = 1024;
const ART_HEIGHT = 1536;
const MARGIN = 108;
const FRAME_WIDTH = ART_WIDTH + MARGIN * 2;
const FRAME_HEIGHT = ART_HEIGHT + MARGIN * 2;
const HEIGHT = 105;
const ART_TO_SCENE = HEIGHT / ART_HEIGHT;

// A fixed envelope around the animated silhouette avoids resizing the
// character on every pose. After a quarter turn the fit axes are swapped.
const PROJECTION_ART_WIDTH = 1200;
const PROJECTION_ART_HEIGHT = 1760;
const PROJECTION_FIT = .83;
export function projectionSpiritScale(width, height) {
  const horizontal = (width - 4) / PROJECTION_ART_HEIGHT;
  const vertical = (height - 4) / PROJECTION_ART_WIDTH;
  return Math.max(0, Math.min(horizontal, vertical)) * PROJECTION_FIT / ART_TO_SCENE;
}

function smooth(lo, hi, value) {
  const f = Math.max(0, Math.min(1, (value - lo) / (hi - lo)));
  return f * f * (3 - 2 * f);
}

const arms = [
  { side: -1, shoulder: [427, 450], elbow: [335, 547], wrist: [248, 592] },
  { side: 1, shoulder: [627, 446], elbow: [711, 554], wrist: [783, 605] }
];

function makeBone(pivot, angle) {
  return { x: pivot[0], y: pivot[1], cos: Math.cos(angle), sin: Math.sin(angle) };
}

function turn(point, bone, amount) {
  const dx = point.x - bone.x;
  const dy = point.y - bone.y;
  point.x += (dx * bone.cos - dy * bone.sin - dx) * amount;
  point.y += (dx * bone.sin + dy * bone.cos - dy) * amount;
}

let lastTime = NaN;
let flight = 0;
let clothFlight = 0;
let movement = 0;
let dancePhase = 0;
let pose;
function poseAt(time, vy, travel, cast) {
  const target = Math.max(-1, Math.min(1, -vy * 3.5));
  // Both windows draw from the same simulation clock. Advance the pose once.
  if (time === lastTime && pose) return pose;
  const dt = time - lastTime;
  if (!Number.isFinite(dt) || dt < 0 || dt > .25) {
    flight = target;
    clothFlight = target;
    movement = travel;
  } else {
    flight += (target - flight) * (1 - Math.exp(-dt / .115));
    clothFlight += (flight - clothFlight) * (1 - Math.exp(-dt / .19));
    movement += (travel - movement) * (1 - Math.exp(-dt / .28));
    dancePhase = (dancePhase + dt * (.164 + .122 * movement)) % 1;
  }
  lastTime = time;

  const rise = Math.max(0, flight);
  const fall = Math.max(0, -flight);
  const stage = danceAt(dancePhase);
  const delayed = danceAt(dancePhase - .058);
  const [shiftX, shiftY, lean, facing, head] = stage.body;
  const [clothX, clothY, clothLean, clothFacing] = delayed.body;
  // At launch, grow into the reach without restarting the dance phrase.
  const bodyEnergy = .82 + .18 * movement;
  const x = shiftX * bodyEnergy;
  const y = shiftY * bodyEnergy - 2.1 * rise + fall;
  const bodyLean = lean * bodyEnergy - flight * .018;
  const bodyTurn = facing * bodyEnergy;
  const footTracks = [stage.leftFoot, stage.rightFoot];
  pose = {
    x, y: y + Math.sin(time * 2.15) * .7,
    lean: bodyLean,
    facing: bodyTurn,
    chest: makeBone([526, 600], .085 * (bodyTurn - clothFacing) - .17 * bodyLean),
    clothLean,
    clothFacing,
    clothLagX: (clothX - shiftX) * 10,
    clothLagY: (clothY - shiftY) * 7 + clothFlight * 17,
    head: makeBone([522, 363], head - .28 * lean),
    arms: arms.map(({ side, shoulder, elbow, wrist }, i) => {
      const [upper, forearm, hand] = i === 0 ? stage.left : stage.right;
      const reach = upper * bodyEnergy + rise * .11 + fall * .035;
      return {
        shoulder: makeBone(shoulder, -side * Math.min(.82, reach + cast * (i ? .12 : .25))),
        elbow: makeBone(elbow, -side * Math.min(.88, forearm + cast * .08 - fall * .065)),
        wrist: makeBone(wrist, -side * (hand + rise * .04 - cast * .17))
      };
    }),
    feet: footTracks.map(([stride, lift, toe, support], i) => {
      const ankle = i ? [531, 1371] : [460, 1376];
      // A supporting toe stays comparatively still as the hips pass over it.
      // The free foot follows the lift and travels into the next step.
      const rootDrift = x / ART_TO_SCENE - bodyLean * (ankle[1] - ART_HEIGHT / 2);
      const plantedX = Math.max(-44, Math.min(44, -rootDrift)) * support;
      return {
        toe: makeBone(ankle, toe - bodyLean * support),
        x: stride + plantedX,
        y: -lift - rise * (1 - support) * 14 + fall * 9 - y / ART_TO_SCENE * support * .30
      };
    })
  };
  return pose;
}

function bindVertex(u, v) {
  const x = u * ART_WIDTH, y = v * ART_HEIGHT;
  const arm = x < 512 ? 0 : 1;
  const { shoulder, elbow, wrist } = arms[arm];
  const out = arm === 0 ? -1 : 1;
  const shoulderDistance = (x - shoulder[0]) * out;
  const elbowDistance = (elbow[0] - shoulder[0]) * out;
  const wristDistance = (wrist[0] - shoulder[0]) * out;
  // Follow the sleeve, elbow and wrist along the painted arm. A straight
  // shoulder-to-hand estimate underweights the outer elbow in deep bends.
  let center = shoulder[1];
  if (shoulderDistance > wristDistance) {
    center = wrist[1] + (shoulderDistance - wristDistance) * .37;
  } else if (shoulderDistance > elbowDistance) {
    center = elbow[1] + (wrist[1] - elbow[1]) * (shoulderDistance - elbowDistance) / (wristDistance - elbowDistance);
  } else {
    center += (elbow[1] - shoulder[1]) * Math.max(0, shoulderDistance) / elbowDistance;
  }
  // Leave the chest and fern collar fixed. Blend at the shoulder seam,
  // and keep the waist ribbons below the arm's influence.
  const armBottom = arm === 0 ? 644 + .39 * (248 - x) : 653 + .37 * (x - 783) + 29 * smooth(799, 835, x);
  const palmWeight = smooth(16, 70, (x - elbow[0]) * out)
    * smooth(510, 560, y) * (1 - smooth(700, 730, y));
  const armWeight = smooth(-25, 53, shoulderDistance)
    * (1 - smooth(43, 86, Math.abs(y - center)))
    * (1 - smooth(675, 719, y));
  return {
    x, y, arm, armWeight: Math.max(armWeight, palmWeight) * (1 - smooth(armBottom, armBottom + 26, y)),
    elbowWeight: smooth(-45, 51, (x - elbow[0]) * out + (y - elbow[1]) * .28),
    wristWeight: smooth(-28, 13, (x - wrist[0]) * out + (y - wrist[1]) * .19),
    headWeight: 1 - smooth(305, 400, y),
    torsoWeight: smooth(325, 470, y) * (1 - smooth(580, 970, y)),
    chestWeight: smooth(250, 390, y) * (1 - smooth(575, 905, y)),
    skirt: smooth(582, 1295, y) * (1 - smooth(1315, 1470, y)),
    ribbon: smooth(645, 925, y) * (1 - smooth(1080, 1190, y))
      * smooth(227, 465, Math.abs(x - 512)),
    leg: smooth(1273, 1402, y) * (1 - smooth(91, 128, Math.abs(x - 490))),
    foot: smooth(478, 502, x)
  };
}

function deformVertex(v, p, flutterTime, moved, foot) {
  moved.x = v.x;
  moved.y = v.y;
  if (v.headWeight > 0) turn(moved, p.head, v.headWeight);
  if (v.armWeight > 0) {
    const bones = p.arms[v.arm];
    // Work from fingertip toward the root so child joints follow parents.
    turn(moved, bones.wrist, v.wristWeight);
    turn(moved, bones.elbow, v.elbowWeight);
    turn(moved, bones.shoulder, 1);
    moved.x = v.x + (moved.x - v.x) * v.armWeight;
    moved.y = v.y + (moved.y - v.y) * v.armWeight;
  }
  if (v.skirt > 0) {
    const fabric = Math.sin(flutterTime + v.x * .006 - v.y * .004);
    moved.x += v.skirt * (fabric * 20 + p.clothLagX + p.clothLean * 195);
    moved.y += v.skirt * (fabric * 13 + p.clothLagY + (v.x - 512) * p.clothFacing * .12);
  }
  if (v.ribbon > 0) {
    const flutter = Math.sin(flutterTime * 1.25 + v.x * .009 - v.y * .008);
    moved.x += v.ribbon * (flutter * 24 + p.clothLagX * .65);
    moved.y += v.ribbon * (flutter * 31 + p.clothLagY * .6);
  }
  if (v.leg > 0) {
    foot.x = v.x;
    foot.y = v.y;
    const [left, right] = p.feet;
    turn(foot, left.toe, 1 - v.foot);
    turn(foot, right.toe, v.foot);
    moved.y += v.leg * (foot.y - moved.y + left.y * (1 - v.foot) + right.y * v.foot);
    moved.x += v.leg * (foot.x - moved.x + left.x * (1 - v.foot) + right.x * v.foot);
  }
  // Foreshorten the silhouette and let the shoulders lead the hips.
  const across = moved.x - 512;
  moved.x += p.facing * (v.headWeight * 27 - v.torsoWeight * 19) - across * Math.abs(p.facing) * .17;
  moved.y += across * v.torsoWeight * p.facing * .115;
  if (v.chestWeight > 0) turn(moved, p.chest, v.chestWeight);
}

// Coordinates on the artwork. Using the same deformation as the mesh keeps
// the trails attached to the actual wrists and slippers through a turn.
const motionTrails = [
  { anchor: bindVertex(188 / ART_WIDTH, 620 / ART_HEIGHT), rgb: '255,222,145', width: 1.25, samples: [] },
  { anchor: bindVertex(844 / ART_WIDTH, 637 / ART_HEIGHT), rgb: '180,251,198', width: 1.25, samples: [] },
  { anchor: bindVertex(463 / ART_WIDTH, 1486 / ART_HEIGHT), rgb: '226,246,170', width: .75, samples: [] },
  { anchor: bindVertex(529 / ART_WIDTH, 1448 / ART_HEIGHT), rgb: '192,249,215', width: .75, samples: [] }
];
const motionPoint = { x: 0, y: 0 };
const motionFoot = { x: 0, y: 0 };
let lastMotionSample = -Infinity;

function sampleMotionTrails(time, pose, articulated) {
  if (time - lastMotionSample < 1 / 32 && time >= lastMotionSample) return;
  const reset = time - lastMotionSample > .25 || time < lastMotionSample;
  const cos = Math.cos(pose.lean), sin = Math.sin(pose.lean);
  for (const trail of motionTrails) {
    const { anchor, samples } = trail;
    if (reset) samples.length = 0;
    if (articulated) deformVertex(anchor, pose, time * 3.4, motionPoint, motionFoot);
    else { motionPoint.x = anchor.x; motionPoint.y = anchor.y; }
    const x = (motionPoint.x - ART_WIDTH / 2) * ART_TO_SCENE;
    const y = (motionPoint.y - ART_HEIGHT / 2) * ART_TO_SCENE;
    const point = { x: pose.x + x * cos - y * sin, y: pose.y + x * sin + y * cos, time, strength: 0 };
    const prior = samples.at(-1);
    if (prior) {
      const speed = Math.hypot(point.x - prior.x, point.y - prior.y) / (time - prior.time);
      point.strength = smooth(4, 65, speed);
    }
    samples.push(point);
    while (samples.length && time - samples[0].time > .48) samples.shift();
  }
  lastMotionSample = time;
}

function drawMotionTrails(ctx, time, pose, articulated) {
  sampleMotionTrails(time, pose, articulated);
  ctx.save();
  ctx.globalCompositeOperation = 'screen';
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  for (const { rgb, width, samples } of motionTrails) {
    for (let i = 1; i < samples.length; i++) {
      const a = samples[i - 1], b = samples[i];
      const opacity = Math.max(0, 1 - (time - b.time) / .48) ** 1.5 * b.strength;
      if (opacity < .018) continue;
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.lineWidth = width * (1.8 + opacity * 2);
      ctx.strokeStyle = `rgba(${rgb},${opacity * .25})`;
      ctx.stroke();
      ctx.lineWidth = width * (.6 + opacity);
      ctx.strokeStyle = `rgba(${rgb},${opacity * .94})`;
      ctx.stroke();
      if (i % 3 === 0) {
        ctx.beginPath();
        ctx.arc(b.x, b.y, width * (.28 + opacity * .62), 0, Math.PI * 2);
        ctx.fillStyle = `rgba(255,248,207,${opacity * .83})`;
        ctx.fill();
      }
    }
  }
  ctx.restore();
}

function createMesh() {
  const canvas = document.createElement('canvas');
  canvas.width = 432;
  canvas.height = 610;
  const gl = canvas.getContext('webgl', { alpha: true, antialias: false, premultipliedAlpha: true });
  if (!gl) return null;

  const program = gl.createProgram();
  const sources = [
    [gl.VERTEX_SHADER, 'attribute vec2 position; attribute vec2 coord; varying vec2 uv; void main(){uv=coord;gl_Position=vec4(position,0.,1.);}'],
    [gl.FRAGMENT_SHADER, `
      precision mediump float;
      varying vec2 uv;
      uniform sampler2D sprite;

      void main() {
        vec4 sampleColor = texture2D(sprite, uv);
        vec3 rgb = sampleColor.rgb / max(sampleColor.a, 0.001);
        float luminance = dot(rgb, vec3(0.2126, 0.7152, 0.0722));
        rgb = clamp(mix(vec3(luminance), rgb, 1.32), 0.0, 1.0);
        // Lift all channels together to retain chroma and embroidered highlights.
        float headroom = 1.0 - max(rgb.r, max(rgb.g, rgb.b));
        float lift = min(0.48 * luminance * (1.0 - luminance), 0.88 * headroom);
        rgb += lift;
        gl_FragColor = vec4(rgb * sampleColor.a, sampleColor.a);
      }
    `]
  ];
  for (const [type, source] of sources) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) return null;
    gl.attachShader(program, shader);
    gl.deleteShader(shader);
  }
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) return null;
  gl.useProgram(program);

  const cols = 38, rows = 50;
  const vertices = [], indices = [], uv = [];
  for (let row = 0; row <= rows; row++) {
    for (let col = 0; col <= cols; col++) {
      const u = col / cols, v = row / rows;
      vertices.push(bindVertex(u, v));
      uv.push(u, v);
      if (row === rows || col === cols) continue;
      const a = row * (cols + 1) + col, b = a + cols + 1;
      indices.push(a, a + 1, b, b, a + 1, b + 1);
    }
  }
  const positions = new Float32Array(vertices.length * 2);
  function attribute(name, values, usage) {
    const location = gl.getAttribLocation(program, name);
    const buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, values, usage);
    gl.enableVertexAttribArray(location);
    gl.vertexAttribPointer(location, 2, gl.FLOAT, false, 0, 0);
    return buffer;
  }
  attribute('coord', new Float32Array(uv), gl.STATIC_DRAW);
  const positionBuffer = attribute('position', positions, gl.DYNAMIC_DRAW);
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, gl.createBuffer());
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint16Array(indices), gl.STATIC_DRAW);
  gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
  gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, true);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, portrait);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.viewport(0, 0, canvas.width, canvas.height);
  gl.clearColor(0, 0, 0, 0);

  const moved = { x: 0, y: 0 };
  const foot = { x: 0, y: 0 };
  return {
    canvas, gl,
    resize(scale, pixelRatio) {
      const requestedWidth = FRAME_WIDTH * ART_TO_SCENE * scale * pixelRatio;
      const width = Math.max(432, Math.min(FRAME_WIDTH, Math.ceil(requestedWidth / 32) * 32));
      if (width === canvas.width) return;
      canvas.width = width;
      canvas.height = Math.round(width * FRAME_HEIGHT / FRAME_WIDTH);
      gl.viewport(0, 0, canvas.width, canvas.height);
    },
    draw(time, p) {
      const flutterTime = time * 3.4;
      for (let i = 0; i < vertices.length; i++) {
        const v = vertices[i];
        deformVertex(v, p, flutterTime, moved, foot);
        positions[i * 2] = (moved.x + MARGIN) * 2 / FRAME_WIDTH - 1;
        positions[i * 2 + 1] = 1 - (moved.y + MARGIN) * 2 / FRAME_HEIGHT;
      }
      gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
      gl.bufferSubData(gl.ARRAY_BUFFER, 0, positions);
      gl.clear(gl.COLOR_BUFFER_BIT);
      gl.drawElements(gl.TRIANGLES, indices.length, gl.UNSIGNED_SHORT, 0);
    }
  };
}

let mesh;
export function renderSpirit(ctx, {
  x, y, scale = 1, time = 0, vy = 0, energy = 0, cast = 0,
  travel = 0, hit = 0, protection = 0, cool = false, rotation = 0, pixelRatio = 1,
  projectionEffects = false
}) {
  const p = poseAt(time, vy, travel, cast);
  const tint = hit > 0 ? '248,173,120' : cool ? '152,212,229' : '169,224,158';
  const artReady = portrait.complete && portrait.naturalWidth > 0;
  if (artReady && mesh === undefined) mesh = createMesh();
  const articulated = artReady && mesh && !mesh.gl.isContextLost();
  ctx.save();
  ctx.translate(x, y);
  if (rotation) ctx.rotate(rotation);
  ctx.scale(scale, scale);
  if (projectionEffects && artReady) drawMotionTrails(ctx, time, p, articulated);
  ctx.translate(p.x, p.y);
  ctx.rotate(p.lean);

  const halo = ctx.createRadialGradient(0, 0, 6, 0, 0, 60);
  halo.addColorStop(0, `rgba(${tint},${.12 + energy * .12})`);
  halo.addColorStop(1, `rgba(${tint},0)`);
  ctx.fillStyle = halo;
  ctx.fillRect(-60, -60, 120, 120);

  if (protection > 0) ctx.globalAlpha = .82 + Math.sin(time * 16) * .15;
  if (artReady) {
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    if (articulated) {
      mesh.resize(scale, pixelRatio);
      // Redraw before each copy: the WebGL backbuffer may be discarded
      // by the browser between draws to the independent projection window.
      mesh.draw(time, p);
      const width = FRAME_WIDTH * ART_TO_SCENE, height = FRAME_HEIGHT * ART_TO_SCENE;
      ctx.drawImage(mesh.canvas, -width / 2, -height / 2, width, height);
    } else {
      const width = HEIGHT * ART_WIDTH / ART_HEIGHT;
      ctx.save();
      ctx.filter = 'saturate(1.32) brightness(1.13)';
      ctx.drawImage(portrait, -width / 2, -HEIGHT / 2, width, HEIGHT);
      ctx.restore();
    }
  }

  for (let i = 0; i < 7; i++) {
    const cycle = (time * (.13 + (i % 3) * .018) + i * .147) % 1;
    const px = (i % 2 ? -1 : 1) * (26 + cycle * 20) + Math.sin(time * .85 + i) * 6;
    const py = 37 - cycle * 77 + Math.sin(i * 3.9) * 9;
    const opacity = Math.sin(cycle * Math.PI) * (.39 + energy * .25);
    ctx.fillStyle = `rgba(223,246,187,${opacity})`;
    ctx.beginPath();
    ctx.ellipse(px, py, i % 3 === 0 ? 1.35 : .85, .8, -.44, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}
