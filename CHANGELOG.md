# 更新日志（Known）

## V12.0.2（2026-04-30）

- 😄[新增]-新增 `Directory.Packages.props`，切换到 NuGet 中央包版本管理，统一 Avalonia、CodeWF.Tools.Core、VC-LTL 与 YY-Thunks 等依赖版本
- 😄[新增]-新增 `pack_libraries.bat`，支持一键打包 `CodeWF.Log.Core` 与 `CodeWF.LogViewer.Avalonia`
- 🔨[优化]-统一 `CodeWF.Log.Core` 与 `CodeWF.LogViewer.Avalonia` 的 NuGet 包输出目录到 `publish/nuget/<Configuration>`
- 🔨[优化]-完善多平台发布脚本与发布配置文件，补齐 `win-x86`、`linux-x64`、`linux-arm64` 的 `pubxml`，并统一发布输出目录命名
- 🔨[优化]-发布脚本增加缺失 `pubxml` 时的命令行回退、`CODEX_NO_PAUSE` 开关，以及发布后自动恢复 `Directory.Build.props` 的平台宏
- 🔨[优化]-示例项目与日志库补充 `net11.0` 目标框架，便于统一 .NET 11 发布与打包流程
- 🐛[修复]-修复 Avalonia 12 下 `CheckBox` 的事件绑定方式，改为使用 `IsCheckedChanged`，恢复 `AvaloniaLogDemo` 构建
- 🐛[修复]-修复 `UpdateAssemblyVersion.ps1` 的编码与注释文本问题，避免不同终端环境下出现乱码

## V11.3.15（2026-04-27）

- 😄[新增]-添加 CHANGELOG.md 更新日志文件，从 2026-04-27 开始记录版本变更
- 😄[新增]-添加一键发布脚本，支持按项目传入 TargetFramework，并覆盖 win-x64、win-x86、linux-x64、linux-arm64 四个平台
- 🔨[优化]-统一测试程序发布配置命名，控制台示例固定 net10.0，Avalonia 桌面示例按平台选择 net10.0 或 net10.0-windows
- 😄[新增]-重做 AvaloniaLogDemo 为专业日志观察台示例，包含业务场景、输出通道、采样级别、实时流量、批量写入和运行指标
- 🔨[优化]-CodeWF.Log.Core 文件日志后台消费改为批次快照刷新，避免防抖任务与消费线程共享集合
- 🔨[优化]-CodeWF.LogViewer.Avalonia 日志 UI 刷新改为批次快照渲染，提升高频日志场景稳定性
- 🔨[优化]-LogView 控件查找和可空性处理，减少运行时空引用风险并保持构建无警告
- 🐛[修复]-修复 LogInfo 未传 uiContent 时 FriendlyDescription 为空导致 UI 友好文本丢失问题
- 🐛[修复]-修复 Logger.FlushAsync 退出时可能未等待后台批次落盘问题
- 🐛[修复]-修复 EnumExtensions 获取未知枚举字段描述时可能出现空引用的问题
