// A classic worker lets the bundled MediaPipe loader use importScripts for its WASM runtime.
let detector;
import('./index-tip.mjs').then(async ({ loadHandDetector }) => {
  detector = await loadHandDetector();
  self.postMessage({ ready: true });
}).catch(() => self.postMessage({ failed: true }));

self.onmessage = async ({ data: { frame, at } }) => {
  let bitmap;
  try {
    bitmap = frame instanceof ImageBitmap ? frame : await createImageBitmap(frame);
    const points = detector.detectForVideo(bitmap, at).landmarks[0] || null;
    self.postMessage({ points, at, aspect: bitmap.width / bitmap.height });
  } catch {
    self.postMessage({ failed: true });
  } finally {
    if (bitmap && bitmap !== frame) bitmap.close();
    frame.close();
  }
};
