# 风扇与彩灯接口 · EOL2

当前接线方向来自项目对话《project (3)》：UNO R3、TL-C12C 四针 PWM 风扇、单独的 WS2812B 彩灯。接口已接入网站；实物接线、固件编译、占空比与风感仍需现场验收。

## 网页到硬件

`麦克风音量 → 速度 → 风力`；`角色跃起 / 聚光 / 共鸣 / 收集成功 → 灯光增强`。收集成功产生约两秒衰减的回应；输出不是音频，也不会把麦克风数据发送到 Arduino。

- 默认只显示预览。进入“硬件接口调试”，连接 Arduino，再勾选“启用实际输出”。
- 使用桌面 Edge / Chrome，地址为 localhost 或 HTTPS。每次连接需要浏览器原生串口选择器；关闭 Arduino IDE 串口监视器以释放端口。
- 串口 115200、8N1；最多每秒 10 帧。整行状态覆盖上一帧，不是需要逐条累积执行的动作。
- 暂停、等待手势、校准、关卡完成时发送零输出；页面隐藏会取消启用。断开、停止按钮会请求归零。
- 模拟音量只供屏幕测试，即使连接串口也只发送零输出。
- 页面卡住、串口断连或进程退出不能依赖浏览器回调，接收端必须实现超时归零。
- 当前没有读取设备 ACK 或转速反馈，因此“已连接 / 已发送”不代表风扇和彩灯实际响应。

```text
EOL2 SET <seq> <fan> <light> <r> <g> <b> <ttl_ms>\n
EOL2 SET 17 80 100 255 205 132 800
EOL2 SET 18 0 0 0 0 0 800
```

| 字段 | 定义 |
|---|---|
| seq | 0–65535 循环序号，供调试；接收端不把它当作累计次数 |
| fan | 0–255，对应请求占空比，不是 RPM |
| light | 0–255，总亮度；RGB 再乘此亮度 |
| r g b | 每项 0–255；森林暖金、遗迹蓝、星海冷白 |
| ttl_ms | 最后一个有效包的有效期；网站为 800 ms |

协议版本为 EOL2，不能直接使用旧实验里的 EOL1 固件。接收端必须校验字段数量、整数范围、行长度；坏包不刷新 watchdog。

前端接口在 `dist/physical-output.js`：`outputLevels()` 负责场景映射，`encodeOutput()` 负责编码，`SerialOutput` 负责串口。新增物体互动可增加脉冲来源，而不修改传输协议。

其他显示模块可订阅本机事件；事件不是发送给实体设备的独立授权：

```js
window.addEventListener('echoes:outputs', ({detail}) => {
  // detail.preview: {fan:0..1, light:0..1, rgb:[r,g,b]}
  // detail.armed / connected / simulated 区分预览与实际启用。
  console.log(detail);
});
```

## UNO 接收示例

打开 `hardware/echoes_uno/echoes_uno.ino`，Arduino IDE 中选择 UNO，安装 **Adafruit NeoPixel** 库，修改 `LED_COUNT` 为实际灯珠数，再编译上传。当前默认 8 颗；示例尚未经本机 Arduino 编译器或实物验证。

| 端口 | 连接 |
|---|---|
| USB | 电脑 ↔ UNO，传送串口指令 |
| D9 | 经**反相开集电极 / 开漏信号电路**接风扇 PWM；不可照搬两线风扇 D5 的 analogWrite 示例 |
| D6 | 串联 470Ω 电阻接 WS2812B DIN |
| 风扇电源 | 按实物铭牌使用外置 12V 电源；TACH 暂不接 |
| 彩灯电源 | 外置 5V 电源；12V 不得接到彩灯或 UNO 信号脚 |
| GND | UNO、控制电路、风扇电源与彩灯电源共地 |

一种反相电路是：D9 → 约 1kΩ → NPN 基极，发射极 → GND，集电极 → 风扇 PWM，基极与发射极间加约 10kΩ。晶体管具体脚序需按所用型号确认。示例已补偿这一级的逻辑反相，不适用于不反相的驱动板。

Timer1 使用 Fast PWM、TOP=639、无分频：`16 MHz / (639+1) = 25 kHz`；占用 D9 / Timer1，不再用于 Servo 等库。固件独立限制风扇请求不超过 166/255、灯亮度不超过 128/255；想提高硬件上限需要同时调整固件。

**零输出仅表示风扇 PWM=0%，不保证 TL-C12C 完全停转。** 不同风扇最低占空比行为不同；若展览必须完全停风，需要另加经确认的电源开关方案。UNO 复位或信号线断开时也不能保证风扇停止，本接口不是独立硬件急停。

## 实物调试顺序

1. 先不连接负载，编译固件；用示波器确认 D9 经反相电路后的约 25 kHz 波形。
2. 只连接彩灯，风力上限设 0；观察灯在动作 / 收集时回应，按暂停与停止检查归零。
3. 再连接风扇，逐步提高风力上限，记录能稳定启动和低速运转的实际范围。这里的百分比是控制量，不是已测得风速。
4. 拔 USB / 关闭页面，检查 watchdog 后的表现；单独确认外部电源供电时的复位行为。
5. 最后合并麦克风：把麦克风放在风扇气流外，防止风噪造成“风越大、音量越大”的反馈。

参考：[Web Serial 官方接口](https://developer.chrome.com/docs/capabilities/serial)、[Arduino PWM](https://docs.arduino.cc/tutorials/generic/secrets-of-arduino-pwm/)、[Noctua 四针 PWM 接口参考](https://cdn.noctua.at/media/Noctua_PWM_specifications_white_paper.pdf)、[NeoPixel 接线建议](https://learn.adafruit.com/adafruit-neopixel-uberguide/best-practices/)。Noctua 文档仅作四针接口参考，不是 TL-C12C 的停转保证。
