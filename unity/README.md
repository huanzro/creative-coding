# 《光的回响》Unity 版

使用 Unity 2022.3.62f3c1 LTS。主游戏的状态推进、章节玩法、光迹、视差滚动和角色画面由 C# 与 Unity 渲染。浏览器负责请求摄像头、麦克风和 USB 串口权限。两者通过字符串消息交换输入与状态。

1. 在 Unity Hub 里添加本目录。它须与同一个仓库的 `dist` 目录相邻。
2. 首次导入后，选择 `Echoes of Light > Prepare Original Art and Scene` 打开场景并导入素材，再按 Play。
3. 在编辑器里，松开空格是张手，按住空格是握拳，Z 是轻声，X 是舒适发声。A/B 选择，R 重试。通过底部按钮从任意章节进入。
4. 安装同版本 WebGL Build Support 后，选择 `Echoes of Light > Build Web Preview`。输出位于 `unity/Builds/Web/`。在该目录运行 `python -m http.server 8780 --bind 127.0.0.1`，用桌面 Chrome 或 Edge 打开 `http://localhost:8780/`。

浏览器试玩可显式开启键盘预览，也可分别开启摄像头和麦克风。先在校准时保持 2 秒安静，再哼声 3 秒。展台设置里可以启用双屏投影，第二个窗口只显示同一个角色。拖到第二块屏后双击全屏，M 切换镜像，H 隐藏提示。

硬件沿用 [EOL2](../docs/HARDWARE.md) 和 [EOL3](../docs/EXHIBIT_WIRING.md) 串口协议。选用与实物烧录固件对应的模式，连接后由用户启用输出。EOL3 支持实体触摸及双灯，EOL2 控制风扇和彩灯。工程保留设备连接测试与现场校准步骤，键盘预览时不会驱动实体输出。

三个场景复用现有作品素材，导入时从仓库自动拷贝，出处和 MediaPipe 许可见 [素材说明](../THIRD_PARTY.md)。网页端调用现有的识别、音量与硬件模块，`Journey.cs` 独立负责玩法。

运行 `pwsh -File unity/Tools/TestJourney.ps1` 可验证 C# 玩法状态和完整章节路径。Unity 编辑器导入、WebGL 构建、浏览器设备权限和接线实测是独立的验证步骤。
