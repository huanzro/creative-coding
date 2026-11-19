# Third-party runtime assets

## Background plates supplied through the user's reference site

The v0.3 background plates were extracted, unmodified, from the inline image resources of the user-provided reference Site: https://echoes-of-light-interactive-1031.tanbowl.chatgpt.site/ . The reference HTML describes the plates as AI-assisted. No claim of original authorship or independent license verification is made for these supplied assets. Original third-party game screenshots from the PDF proposal are not used.

- `dist/assets/chapter1-0.png`: ancient arches, RGBA foreground / middle layer.
- `dist/assets/chapter1-1.png`: distant forest valley, RGB far layer.
- `dist/assets/chapter1-2.png`: flower banks, RGBA foreground layer.
- `dist/assets/chapter2-0.png`: floating geometric ruins.
- `dist/assets/chapter3-0.png`: cosmic starfield.

Exact source pages are recorded in `dist/assets/sources.json`; downloaded bytes are recorded in `assets-sha256.txt`. No scripts from the reference Site are executed by this game. Time-of-day grading, scene movement, effects, gameplay, character and score integration are implemented locally. The reference Site itself has not been changed.

## Hand recognition runtime

- MediaPipe Tasks Vision: `@mediapipe/tasks-vision@0.10.22-rc.20250304`, Apache License 2.0. Source: https://github.com/google-ai-edge/mediapipe . Distribution: https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.22-rc.20250304/ . Bundled files are unmodified.
- Hand Landmarker model: https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task . Official model information: https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker . See the upstream model documentation for model limitations.
- Apache license text is retained in `dist/vendor/LICENSE`.

No runtime CDN, analytics, external fonts, image upload, or video recording is used.
