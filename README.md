# Creative Coding · 光的回响

同一位小精灵穿过森林、遗迹和星海。握拳收束，张手展开，呼吸般的声音改变世界如何回应。人物保持在画面中央，由背景、目标和光迹表现行进。

## 启动与输入

使用 Python 3 启动，再用桌面 Edge 或 Chrome 打开终端显示的本机地址。

```sh
python server.py --open
```

1. 开启摄像头，把手放入画面，先完整张开。若张手和握拳难以稳定区分，可重新校准手势。
2. 开启麦克风，先安静 2 秒，再舒适地哼声 3 秒。系统只测音量强度。设备未就绪、丢手或断连时会暂停并保留进度。
3. 按一次空格代表一次点击。连接展台 Arduino 后，触摸 A/B 的按下沿使用相同的点击入口。关闭或开启设备、暂停和章节设置用侧栏按钮。

「回声庭院」会用三个小试验教会张手、握拳、声音和点击。通过后进入森林，选择长而稳的林荫路，或握拳护光穿过风带。遗迹先记录自己的动作，再与回声做相反的动作。星海则用同相或反相回应来波，最后由世界接住玩家发出的三道波。「展台设置与章节」可以重试当前小段、选择章节或恢复本机检查点。

## 投影与展台

点击「开启双屏投影」，把人物窗口拖到第二块屏幕。双击全屏，H 隐藏人物窗口提示，M 左右镜像。把主窗口和人物窗口都保持可见。侧栏的「纯黑人物展示」可单独查看角色，切回后继续同一段旅程。

- [麦克风校准与音量反馈](docs/VOLUME_TUNING.md)
- [EOL2 风扇和彩灯接口](docs/HARDWARE.md)
- [EOL3 双灯、舵机与触摸接线](docs/EXHIBIT_WIRING.md)
- 展台调试页 `/exhibit-hardware.html` 可单独测试灯、舵机与触摸。网页默认关闭真实输出，须在相应面板明确启用。一个串口只交给一个页面使用。

## 验证与部署

运行 `node --test "tests/*.test.js"`（或 `npm test`）验证状态机、手势门控、声音校准、旧资源和串口协议。可选浏览器检查用 `node tests/browser-smoke.cjs`，需要 Playwright 和桌面 Edge。先启动 `python server.py --port 8776`，用 `ECHOES_BROWSER_TEST_ORIGIN` 可指定另一个本机或 HTTPS 地址。

`dist/` 可在本机运行或部署到支持 HTTPS 的静态网站托管。三个历史章节的 `chapter1-offline.html` 至 `chapter3-offline.html` 路径仍可访问。素材来源见 [THIRD_PARTY.md](THIRD_PARTY.md)，真人和自动验证的区分见 [VERIFICATION.md](VERIFICATION.md)。
