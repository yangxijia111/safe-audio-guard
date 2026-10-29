# 硬件验收清单 / Hardware Acceptance Checklist

以下测试必须在真实 Windows 11 + 实体音频设备上人工完成。**测试时不要佩戴耳机**；先暂停音乐，将设备音量调高后再断开/重连，观察系统音量 UI 与 `%LOCALAPPDATA%\SafeAudioGuard\app.log`。

> 当前状态：**全部未执行（Not yet performed）**。已在 Windows 11 x64 上完成编译与启动安全检查实测（74% → 45%），但尚未连接实体蓝牙耳机验证。

| 编号 | 场景 | 预期 |
|---|---|---|
| T01 | 不佩戴耳机，设备音量为 90%，断开再连接蓝牙 | 短时间内回落到 45% 或更低，日志有事件与调整记录 |
| T02 | 连接前音量为 20%，重新连接 | 保留 20%，不会被升到 45% |
| T03 | 连接后手动提高到 70%，不拔插/不切换设备 | 不会被持续压低（仅连接时生效） |
| T04 | 切换到 HDMI、USB 或普通有线输出设备 | 活动输出端点若超过 45% 会被限制 |
| T05 | 切换为 USB 麦克风输入 | 不修改输入设备的录音增益 |
| T06 | 暂停保护后重新连接设备 | 不执行自动音量调整 |
| T07 | 恢复保护或执行“立即检查所有活动输出” | 触发对现有活跃输出端点的安全检查 |
| T08 | 设置开机自启，注销后重新登录 | 程序自动进入托盘且只有单实例 |
| T09 | 取消自启并重新登录 | 程序不应被该启动项启动 |
| T10 | 设备快速断开重连 10 次 | 无异常退出、无重复实例、无持续占 CPU |
| T11 | 睡眠/唤醒后重连蓝牙 | 尽可能捕获设备状态或默认端点事件 |
| T12 | 不同蓝牙耳机或支持 Absolute Volume 的型号 | 核实系统音量与硬件音量实际表现，记录首包峰值 |

## 记录要求

- 每次测试记录：时间戳、设备型号、事件原因、调整前/后音量、是否回读成功。
- 若首包峰值仍明显，应在日志与 Issue 中如实记录，不得声称“零风险”。

---

# Hardware Acceptance Checklist (English)

These tests must be performed manually on a real Windows 11 machine with physical audio devices. **Do not wear headphones while testing.** Pause playback first, raise the device volume, then disconnect/reconnect and observe both the system volume UI and `%LOCALAPPDATA%\SafeAudioGuard\app.log`.

> Status: **all not yet performed.** Compilation and the startup safety check (74% → 45%) have been verified on Windows 11 x64, but no physical Bluetooth headset has been tested.

| # | Scenario | Expected |
|---|---|---|
| T01 | Without wearing headphones, device volume 90%, disconnect and reconnect Bluetooth | Volume falls back to ≤45% shortly after reconnect; log shows the event and adjustment |
| T02 | Volume 20% before reconnect | 20% is preserved, never raised to 45% |
| T03 | After connecting, manually raise to 70%, no replug/switch | Not continuously suppressed (connect-time only) |
| T04 | Switch to HDMI, USB, or plain wired output | Active render endpoint above 45% is clamped |
| T05 | Switch to a USB microphone input | Input device recording level is not modified |
| T06 | Reconnect while protection is paused | No automatic volume adjustment |
| T07 | Resume protection or run “Check all active outputs” | Safety check runs over existing active render endpoints |
| T08 | Enable auto-start, sign out and back in | App enters the tray automatically, single instance only |
| T09 | Disable auto-start and sign back in | App is not started by that entry |
| T10 | Disconnect/reconnect rapidly 10 times | No crash, no duplicate instance, no sustained CPU usage |
| T11 | Reconnect Bluetooth after sleep/wake | Device-state or default-endpoint events are captured where possible |
| T12 | Different Bluetooth headsets or models with Absolute Volume | Verify actual system vs. hardware volume behavior; record any first-packet peak |
