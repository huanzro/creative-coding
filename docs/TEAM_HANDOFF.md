# 《光的回响》队友交接

## 先打开游戏

Windows x64 安装包、便携包和调试包在同一页发布。直接运行 `Echoes-of-Light-Setup-Windows-x64.exe` 选择安装目录。便携展示用 `Echoes-of-Light-Windows-x64.zip`，排练和定位问题用 `Echoes-of-Light-Debug-Windows-x64.zip`。解压后运行 `Echoes of Light.exe`，保留同目录的游戏数据和运行库。调试包用 Unity Development Build 构建，请一并保留随包生成的调试文件。[GitHub Releases](https://github.com/huanzro/creative-coding/releases)

Mac 下载 `Echoes-of-Light-macOS-Universal.zip`，在 Finder 解压后打开 **Echoes of Light.app**。该包支持 Apple 芯片和 Intel 芯片。摄像头和麦克风在 F1 设置里启用。带 Arduino 和独立人物投影窗的实体展示用 Windows 版。[GitHub Releases](https://github.com/huanzro/creative-coding/releases)

从底部「试玩」进回声庭院。按住空格约半秒再松开模拟握拳和张手。按住 Z 或 X 试两档音量，用 A/B 模拟两个触点。遗迹也可用左右方向键。星海按住鼠标左键拖动模拟食指。F1 设置，F2 迷宫地图，R 重试，Esc 跳过片头。整条键盘流程可离开实体设备排练。

## 打开可修改的项目

1. 克隆 [creative-coding](https://github.com/huanzro/creative-coding)，确认和队友用同一个发布标签或提交。
2. 在 Unity Hub 安装或选择 **2022.3.62f3c1 LTS**。构建 Windows x64 时备好 Windows Build Support。
3. 在 Unity Hub 添加仓库的 `unity/` 文件夹。保留同一个检出目录的 `dist/`，Unity 导入会读取共享素材和手部识别文件。
4. 首次打开运行 **Echoes of Light → Prepare Original Art and Scene**。确认 `Assets/Scenes/Journey.unity` 打开，再按编辑器顶部 **Play**。需要键盘输入时，点一下 **Game** 画面。

在编辑器用 **Echoes of Light → Build Windows Preview** 生成展示版，用 **Build Windows Debug** 生成调试版。生成位置分别是 `unity/Builds/Windows/` 和 `unity/Builds/WindowsDebug/`。压缩时要包含整个构建目录。

| 要改什么 | 从哪里读 |
| --- | --- |
| 庭院、关卡条件、状态和存档 | `unity/Assets/Scripts/Journey*.cs` |
| 三维背景、人物姿态、两个窗口 | `EchoesWorld.cs`、`WorldSet.cs`、`SpiritFrame.cs`、`SpiritRig.cs`、`EchoesProjectionView.cs` |
| 星海笔迹与纪念图 | `StarInkSpace.cs`、`StarRibbonRenderer.cs`、`JourneyStars.cs` 和星海尾声相关文件 |
| 灯、风扇、Arduino 两触点 | `WindowsExhibit.cs`、`ExhibitProtocol.cs`、`hardware/echoes_exhibit_uno/` |
| 资源来源和 AI 辅助记录 | [素材说明](../THIRD_PARTY.md) 与 [AI 使用记录](AI_USE.md) |

运行参数、双屏和设备操作见 [设置详解](UNITY_SETTINGS.md)。[中文版游玩指南](../output/pdf/echoes-instructions-zh.pdf) · [English player guide](../output/pdf/echoes-instructions-en.pdf) · [中文版设计与制作记录](../output/pdf/echoes-design-and-process-zh.pdf) · [English design and development](../output/pdf/echoes-design-and-process-en.pdf)

## 用同一份版本排练

| 从哪里开始 | 核对什么 |
| --- | --- |
| 回声庭院 → 序幕 | 手势、两档声音和两个触点均有响应。看序幕从林下到遗迹再到星空，片名渐显后可接森林。 |
| 森林左路 | 静音张手仍走迷宫，A/B 连续转向，F2 能显示路线和朝向。上下两个根杯都能种。 |
| 森林右路 | 三个错误入口各自复归门廊。能探索进隐蔽通道、给机械心脏种光并进入遗迹。食人花、蜘蛛、救援以及零萤火失败后可再开始。 |
| 遗迹主线 | 接近藏品自动拾取。普通分支变短。开箱自动填空位，满 15 件才出现处理选项。完成足够转场后，最终长隧道尽头撞墙。记录两种交易选择与各自的过场。 |
| 星海主线 | 连续绘画、A/B 分支图案、失去指尖暂续笔、音量加速。结束后核对完整金色轨迹、片名、音效、亮度与图片。 |
| 双窗口 | 左右和上下活动中比对角色的位置。再检查序幕萤火、森林陷阱、遗迹宝箱发光、星海笔迹和肖像轮廓。 |
| 下载测试 | 手机和 Windows 展台机连到同一可互通的 Wi-Fi，扫结尾二维码保存 PNG。断网或访客网络隔离时，打开本机保存目录取图。 |

排练场景和断点用于定位，正式体验始终从庭院开始。按 **F1 → 森林调试断点** 可直接进根杯、陷阱、三扇门或机械心脏。遗迹可用 **F1 → 遗迹撞墙断点**。R 重跑当前断点，「恢复」读取正式存档。

## 展台复测

先在本机完成无硬件排练，再连接 Arduino、摄像头和麦克风。清楚记录设备型号、两个窗口位置、所跑关卡分支、二维码下载结果及灯色。灯光对照见 [设置详解](UNITY_SETTINGS.md)。

现场校准时先把主画面和人物窗分别拖到指定屏幕，调整镜像和方向。用游戏里的覆盖框确认星海笔迹的拼接范围，再调整人物窗大小和主画面亮度。保持摄像头的本机识别页打开，校准后切回游戏。Arduino 据 [接线和烧录说明](EXHIBIT_WIRING.md) 一次连接一个串口。现场使用前需要复核两个实体触点、灯条颜色、风扇、同网扫码和真实光路。

## 提交更新

多人改动前先同步 GitHub。代码、素材和正式文档进源码仓库，安装和调试包放本次 GitHub Release。Unity 的 `Library/`、`Temp/` 和现场日志留在本地。任何验收截图都标明是程序模拟、设备回执还是实体展台实测。[工作区协作规则](../AGENTS.md)
