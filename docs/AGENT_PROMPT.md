# Codex / Zcode / Trae 开发执行提示词

你是一名熟悉 Windows Core Audio、C# COM Interop 和 WinForms 的高级 Windows 桌面工程师。请接手当前目录里的 `SafeAudioGuard` 完整源码，**在 Windows 11 x64 上编译、修复、测试并交付正式 EXE**。已有初版代码，优先在现有工程上修复，不要无故重写成 Electron 或引入服务器。

## 已确定需求（不可擅自修改）

1. 目标机器：Windows 11 x64（普通 x64 PC 即可，无特殊硬件要求）。
2. 所有输出设备（蓝牙/3.5mm/USB/HDMI 等）连接或成为默认输出时，若设备音量超过 **45%**，降为 **45%**；低于等于 45% 不修改；不改静音、不改麦克风。
3. 只在设备连接、激活、默认输出变化、程序启动、恢复保护以及手动检查时触发；**不要**在正常播放期间持续限制用户主动调高的音量。
4. 纯后台托盘，支持暂停、恢复、手动检查、日志、退出、**HKCU 开机自启**（正式 EXE 第一次运行默认开启，可关闭），禁止索要管理员权限。
5. 没有额外 NuGet 依赖，优先 C# .NET 10 + WinForms + Windows Core Audio COM；如因明确技术原因更换 NAudio，需解释并固定版本。
6. 要解决设备事件早于音量初始化、驱动重新恢复音量的问题，使用 `OnDeviceAdded`、`OnDeviceStateChanged(ACTIVE)`、`OnDefaultDeviceChanged(Render)` 和 0、60、180、450、900、1800ms 的短窗口复查。必要时实测调整重试间隔。系统回调只入队，遵守 Microsoft IMMNotificationClient 规则。
7. 禁止声称普通应用可以完全消除蓝牙连接瞬间的首包音量峰值；需说明驱动、蓝牙 Absolute Volume、设备硬件音量与 WASAPI 独占模式限制。

## 工作步骤

### P0：编译验证
- 执行 `dotnet --info`，`dotnet run --project SafeAudioGuard.PolicyTests -c Release`，`./build.ps1`。
- 检查 COM GUID、接口方法 vtable 顺序、`MarshalAs`、对象释放与回调生命周期。
- 修复所有构建错误与警告，不允许“代码看起来可用就算验收”。

### P1：安全行为验证
- 在非佩戴耳机的条件下执行 README T01–T12 手工验收。
- 记录 `app.log` 中实际检测事件、时间戳、调整前/后的音量；检查音量回读。
- 证明只对 `DataFlow.Render` 生效、不会误改输入设备。
- 对短时拔插、快速切换默认设备、休眠恢复测试事件时序和不会僵死。

### P2：竞态与健壮性
- 验证先注册回调再枚举活跃输出。
- 遵循微软回调非阻塞规则；绝不在 COM 系统回调中进行 COM 操作或文件 IO。
- 检查多设备同一时刻变化下的代际去重、错误重试、暂停后仍在队列中的任务、防止退出后调用 UI。
- 检查音频服务崩溃/重启、设备 ID 变化、Windows 睡眠唤醒的恢复能力，补充事件丢失的兜底策略但保持**非连续限音**语义。
- 如果首包峰值仍明显，提出可选的更保守保护模式，不得偷偷静音或修改用户未同意的设备设置。

### P3：发布与可安装性
- 将正式 EXE 输出到 `dist/SafeAudioGuard.exe`，实现不需要安装 .NET Runtime 的 x64 单文件发布。
- 在全新普通 Windows 用户下核查注册表启动项与开机、取消自启、移动程序目录后的行为。
- 不得引入外网通信、遥测或敏感信息采集；日志不能包含完整设备 ID。

### P4：交付
- 交付能运行的 EXE、全部项目源码、真实验收记录、已知问题、修复清单。
- 汇报采用 PASS / FAIL / 未测试，不允许将“编译通过”等同于“已在蓝牙耳机实测保护成功”。
- 仅在实体设备上确认之后，才能写“运行测试通过”。

## 官方依据
- https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immnotificationclient
- https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-registerendpointnotificationcallback
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmastervolumelevelscalar
- https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-volume-controls
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/
