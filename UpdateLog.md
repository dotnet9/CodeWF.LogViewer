# 更新日志（Known）

## 12.0.4.2 (2026-06-08)

- 🎨[优化]-再次收敛根目录 `logo.svg`、`logo.png`、`logo.ico`，移除外层深色底板，将日志面板本身作为主体图标，提升小尺寸显示清晰度。

## 12.0.4.1 (2026-06-08)

- 🎨[优化]-重新设计根目录 `logo.svg`、`logo.png`、`logo.ico`，以简洁的日志查看面板、日志行和级别圆点表达 C# 日志组件与 Avalonia 视图展示定位，并保持小尺寸图标可辨识。
- 🔨[优化]-统一 NuGet 打包输出目录为 `artifacts\packages`，并优化 `pack.bat` 在自动化打包时不弹出资源管理器。
- 🔨[优化]-将 NuGet 包描述收敛为简体中文，并分别匹配核心日志库与 Avalonia 日志查看控件的实际定位。

## 12.0.3.3 (2026-06-08)

- 🔨[优化]-补齐根目录 logo.svg、logo.png、logo.ico 三件套，子工程通过 MSBuild Link 引用根 logo，避免维护多份图标副本。
- 🔨[优化]-统一目标框架：NuGet 包项目支持 `net8.0;net10.0`，Demo、App、测试与内部应用项目升级到 `net11.0` / `net11.0-windows`。
- 🔨[优化]-保留运行时帮助、Markdown 示例、内置备忘录和业务设计文档，仅收敛仓库级重复文档入口。

## 12.0.3.2 (2026-06-08)

- 统一版本号维护入口，只在仓库根目录 `Directory.Build.props` 中定义 `<Version>`。
- 清理英文/双语文档入口，后续仅维护简体中文文档。
- 完善 NuGet 发布配置，补充 Source Link、符号包和标签格式规范。


## V12.0.2（2026-04-30）

- 😄[新增]-新增 `Directory.Packages.props`，切换到 NuGet 中央包版本管理，统一 Avalonia、CodeWF.Tools.Core、VC-LTL 与 YY-Thunks 等依赖版本
- 😄[新增]-新增 `pack_libraries.bat`，支持一键打包 `CodeWF.Log.Core` 与 `CodeWF.LogViewer.Avalonia`
- 🔨[优化]-统一 `CodeWF.Log.Core` 与 `CodeWF.LogViewer.Avalonia` 的 NuGet 包输出目录到 `publish/nuget/<Configuration>`
- 🔨[优化]-完善多平台发布脚本与发布配置文件，补齐 `win-x86`、`linux-x64`、`linux-arm64` 的 `pubxml`，并统一发布输出目录命名
- 🔨[优化]-发布脚本增加缺失 `pubxml` 时的命令行回退、`CODEX_NO_PAUSE` 开关，以及发布后自动恢复 `Directory.Build.props` 的平台宏
- 🔨[优化]-示例项目与日志库移除 .NET 预览目标框架，统一回到稳定 `net10.0` 发布与打包流程
- 🐛[修复]-修复 Avalonia 12 下 `CheckBox` 的事件绑定方式，改为使用 `IsCheckedChanged`，恢复 `AvaloniaLogDemo` 构建
- 🐛[修复]-修复 `UpdateAssemblyVersion.ps1` 的编码与注释文本问题，避免不同终端环境下出现乱码

## V11.3.15（2026-04-27）

- 😄[新增]-添加 UpdateLog.md 更新日志文件，从 2026-04-27 开始记录版本变更
- 😄[新增]-添加一键发布脚本，支持按项目传入 TargetFramework，并覆盖 win-x64、win-x86、linux-x64、linux-arm64 四个平台
- 🔨[优化]-统一测试程序发布配置命名，控制台示例固定 net10.0，Avalonia 桌面示例按平台选择 net10.0 或 net10.0-windows
- 😄[新增]-重做 AvaloniaLogDemo 为专业日志观察台示例，包含业务场景、输出通道、采样级别、实时流量、批量写入和运行指标
- 🔨[优化]-CodeWF.Log.Core 文件日志后台消费改为批次快照刷新，避免防抖任务与消费线程共享集合
- 🔨[优化]-CodeWF.LogViewer.Avalonia 日志 UI 刷新改为批次快照渲染，提升高频日志场景稳定性
- 🔨[优化]-LogView 控件查找和可空性处理，减少运行时空引用风险并保持构建无警告
- 🐛[修复]-修复 LogInfo 未传 uiContent 时 FriendlyDescription 为空导致 UI 友好文本丢失问题
- 🐛[修复]-修复 Logger.FlushAsync 退出时可能未等待后台批次落盘问题
- 🐛[修复]-修复 EnumExtensions 获取未知枚举字段描述时可能出现空引用的问题
## 2026-06-08 仓库规范整理

- 统一文档维护入口：每个仓库只保留根目录 `README.md` 和根目录 `UpdateLog.md`，清理重复日志、英文文档和语言切换入口。
- 统一版本维护入口：包版本只在仓库根目录 `Directory.Build.props` 的 `<Version>` 节点维护，移除散落的程序集版本配置。
- 不再维护 `global.json`，SDK 选择交给本机或 CI 环境；NuGet 包和应用的目标框架在项目文件中明确声明。
- 统一 NuGet 包文档入口：包 README 统一引用仓库根 `README.md`，更新日志统一引用仓库根 `UpdateLog.md`。
