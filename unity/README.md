# 《光的回响》Unity 版

使用 Unity 2022.3.62f3c1 LTS。主游戏的状态推进、章节玩法、光迹、视差滚动和角色画面由 C# 与 Unity 渲染。导出到浏览器时，网页负责请求摄像头、麦克风和 USB 串口权限，再把设备输入交给 Unity。

1. 在 Unity Hub 里添加本目录。它须与同一个仓库的 `dist` 目录相邻。
2. 首次导入后，选择 `Echoes of Light > Prepare Original Art and Scene` 打开场景并导入素材，再按 Play。
3. 在编辑器里，松开空格是张手，按住空格是握拳，Z 是轻声，X 是舒适发声。A/B 选择，R 重试。正常动线是**回声庭院 → 序幕 → 森林 → 遗迹 → 星海**，底部四个按钮可以单独排练各场景。按 **F1** 或点右上角**游戏设置**可调背景、打开纯黑人物窗或直接预览序幕。序幕可用 **Esc** 跳过并进入森林。
4. 安装同版本 WebGL Build Support 后，选择 `Echoes of Light > Build Web Preview`。输出位于 `unity/Builds/Web/`。在该目录运行 `python -m http.server 8780 --bind 127.0.0.1`，用桌面 Chrome 或 Edge 打开 `http://localhost:8780/`。

Unity Play Mode 中按 **F1 → 打开独立人物窗口**，也可以用编辑器顶部的 **Echoes of Light → Character Projection**。把浮动窗口标题栏拖到第二块屏幕。**人物大小**（35%–140%，受窗口边缘约束）与**光效延展**（0.65–2.45 倍）可以分别调，设置保存在本机。人物窗的 **M** 切换镜像、**R** 切换横放和直立、**H** 隐藏控制条。关掉人物窗会让精灵回到 Game 视图。两个窗口读取同一个姿态和飞行路径，主画面保留较淡的光迹与光圈。

握拳再张手会发出光圈，精灵随光上升，展开双臂后缓慢滑翔到画面下沿。按住 **空格＋X** 蓄力约半秒，再保持 X、松开空格，三维精灵会踮脚转身，滑翔余光也更长。声音输入用音量加速。张手、握拳、轻声、持续发声和 A/B 触摸都有随三章变化的动作。

在 Unity 游戏设置中可开启原生麦克风。首次使用时安静 2 秒，接着舒适地哼声 3 秒。本机保存该麦克风的校准，重新开启时复用。真实手势识别仍走浏览器，Unity Play Mode 内用空格和“模拟手离开”覆盖相同的输入状态。

Windows 的 Unity 游戏设置可填写设备管理器里的 COM 号并连接 Arduino。A/B 输入和双灯、风扇共用一个 EOL3 串口。双灯和风扇各有启用开关，组合固件报告支持后才可启用风扇。暂停、失去窗口焦点、停止 Play Mode 或串口失联会取消输出。旧 EOL3 固件可继续测试触摸和双灯。接线与烧录见 [EOL3 展台说明](../docs/EXHIBIT_WIRING.md)。

WebGL 导出支持摄像头和麦克风权限，按设备复用校准。导出版可用浏览器的双屏投影按钮打开人物窗，拖到第二块屏后双击全屏。D 盘编辑器里的独立窗口用于本机调试，双实体屏的摆位和投影仍需现场验证。

## 跨会话进度

- [第 16 版玩法设置与组合固件](https://github.com/huanzro/creative-coding/commit/e7a8f4a37823369786e382cd06ac68e39cd4a4cd)。调整章节、时段、背景速度，合并串口。Unity 复用同一 EOL3 固件。
- [第 15 版输入体验](https://github.com/huanzro/creative-coding/commit/fd5321324ee44a8e30657876e04d902419053ac4)。开局后手离开时旅程继续，未检测到手的时段不会当成张手或握拳；握拳蓄力清除。声音加快画面滚动，关卡计时保持原速。
- [第 14 版场景响应](https://github.com/huanzro/creative-coding/commit/d5ad9549d848ca8a318b93e1e39ba70e7dd2dbf2)。三章张手醒光、握拳压暗并减慢背景，握拳后张手时扩散光波，声音带动视差和粒子。
- [精灵和双屏投影](https://github.com/huanzro/creative-coding/commit/8586e2650970245beb3fefe7e76aaadcc47b5815)。Unity 复用原立绘、手肘、手腕、脚尖、裙摆和飘带的动作，增加独立人物窗与高速释放转身。
- [下一版关卡与角色演出草案](../docs/UNITY_LEVEL_DESIGN.md)。
- [谁在引路 · 序幕分镜、触点与三章设计](../docs/HUMAN_MACHINE_INTERACTION.md)。

游戏背景复用现有作品素材，出处和 MediaPipe 许可见 [素材说明](../THIRD_PARTY.md)。序幕三张专用背景随 Unity 工程导入。网页端沿用识别、音量和硬件输入，Unity 的 `Journey.cs` 管理试玩、关卡、飞行和萤火。

运行 `pwsh -File unity/Tools/TestJourney.ps1` 可验证 C# 玩法状态和完整章节路径。Unity 编辑器导入、WebGL 构建、浏览器设备权限和接线实测是独立的验证步骤。
