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

## 3D world surface textures

The following CC0 diffuse maps from Poly Haven are used on procedurally built meshes in `unity/Assets/Resources/WorldMaterials/`. [Poly Haven license](https://polyhaven.com/license)

| Local asset | Source |
| --- | --- |
| `Bark.jpg` | [Bark Brown 01](https://polyhaven.com/a/bark_brown_01) |
| `Stone.jpg` | [Mossy Rock](https://polyhaven.com/a/mossy_rock) |
| `Ground.jpg` | [Forest Leaves 02](https://polyhaven.com/a/forest_leaves_02) |

## Selectable 3D characters

`unity/Assets/Resources/GuestSpirits/` contains two CC0 1.0 characters from Kay Lousberg's [KayKit Adventurers source](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/tree/672074b73ba276876a19e8816ecdc5241817ab47/addons/kaykit_character_pack_adventures). The source license is included as `KayKit-License.txt`. The FBX exports retain their UVs and skin weights, with two hood faces triangulated for Unity import. Animation takes were removed because both models use the game's shared hand, flight and cutscene poses. Both capes follow those poses too.

| Local model | Playable character | Source texture |
| --- | --- | --- |
| `RogueHooded.fbx` | 兜帽旅人 | `rogue_texture.png` |
| `Mage.fbx` | 星纹术士 | `mage_texture.png` |

### Additional models reviewed

- Quaternius Modular Character Outfits – Fantasy has interchangeable humanoid outfits suited to a consistent forest cast, including clothing for further customization. [GitHub asset mirror](https://github.com/agentkaerf/FreeModels) · [Creator's CC0 pack](https://quaternius.com/packs/modularcharacteroutfitsfantasy.html)
- Quaternius RPG Character Pack offers six textured fantasy characters. Its Ranger and Monk silhouettes could suit an outdoors guide or a ruins caretaker. [GitHub copy of the model files](https://github.com/euuuuuuan/cairnfall-public/tree/main/assets/vendor/quaternius_rpg) · [Creator's CC0 pack](https://quaternius.com/packs/rpgcharacters.html)
- GDQuest Open 3D Mannequin includes editable Blender artwork and baked animations, with attribution required for reuse of art. The figure is a neutral base for custom costumes and proportion studies. [GDQuest source and licenses](https://github.com/gdquest-demos/godot-3d-mannequin#licenses)
