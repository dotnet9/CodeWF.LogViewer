# 更新日志（Known）

## 12.1.0.18 (2026-07-24)

- 💥[重构]-三个 NuGet 包统一目标 `net10.0`，删除 `UserLogEntry`、`UserLogFeed`、`UserLogPayload` 和 `UserLogMode`，所有通道改用完整不可变 `CodeWFLogEvent`。
- 😄[新增]-File 使用可原子更新的独立 `OutputTemplate`；Console、LogView 和通知共享可原子更新的 `LineTemplate`，`{UserMessage}` 为空白时回退 `{Message}`。
- 😄[新增]-MEL Provider 使用实例级 Pipeline，支持完整 State、Scope、Activity、EventId、LoggerMessage、异常快照、ContentRoot 路径和可选静态 Logger 桥接。
- 🔨[优化]-文件默认单文件 1000 MB、保留 30 天；队列增加满载策略、超时、按级别丢弃健康统计，Feed 慢订阅不再阻塞 Pipeline。
- 🔨[优化]-LogView 默认 Information 至 Critical；通知只按 MinimumLevel 判断，增加 InApp 三条上限、DesktopWindow 单窗口和容量 100 的展示队列。
- 😄[新增]-Avalonia Demo 提供自动应用的 LineTemplate/OutputTemplate 预设和手工编辑，不再需要“应用”按钮；MultiProvider Demo 只编辑 CodeWF LineTemplate；另含纯 appsettings Web API Demo。
- 🐛[修复]-LogView 与通知窗口打开日志目录改用应用/控件目录上下文，不再要求静态 Logger 已初始化，兼容 Serilog 文件 + CodeWF UI 的组合。
- 🔨[优化]-MEL Capture 配置在 Runtime 启动时固定快照，避免外部修改 Options 形成未声明的局部热更新；多个 Host 的 CodeWF Console 输出按整行串行化，避免颜色和文本交错。
- ✅[验证]-增加 Core/MEL 自动化测试、稳定 .NET 10 构建、Native AOT 执行和 Avalonia trim publish smoke。

## 12.1.0.16 (2026-07-23)

- 🔨[优化]-桌面通知确认按钮新增独立按下态资源，并在按钮模板层覆盖悬浮和按下背景，避免被宿主主题回退为灰色。
- 🔨[优化]-移除桌面通知按钮 ToolTip，避免宿主主题导致提示样式错乱；键盘快捷键保持不变。

## 12.1.0.13 (2026-07-23)

- 🐛[修复]-应用在 `App.axaml` 中替换根资源字典后，创建通知窗口前重新确认默认通知资源，避免窗口透明、尺寸失效和文字退回默认样式。
- 🔨[优化]-Avalonia Demo 默认使用组件自动注册的通知资源，并提供按钮动态切换自定义资源覆盖。

## 12.1.0.12 (2026-07-23)

- 🐛[修复]-修复 `LogViewSubscription` 在后台线程读取 Avalonia 属性，并在异常清理阶段同步重入直至栈溢出的问题。
- 🔨[优化]-`LogView` 只在 UI 线程读取刷新周期，后台订阅使用线程安全的值快照和单一异步调度入口。
- 💥[重构]-`LogNotifications` 改为在 `App.axaml` 配置的应用级能力，动态选择活动窗口或主窗口，一个应用只订阅一次。
- 🔨[优化]-视图刷新和通知调度的内部异常使用自诊断输出隔离，不再反向写入日志管线或同步重试。
- 🐛[修复]-桌面通知窗口改为主窗口的 owned window，主窗口关闭时立即级联关闭通知，避免倒计时窗口延迟进程退出。

## 12.1.0.8 (2026-07-23)

- 💥[重构]-重新定义日志输出语义：普通方法写文件并进入用户出口，`*ToFile` 只写文件；异常对象不再进入 UI 数据模型。
- 😄[新增]-`Logger.Initialize`、`LoggerOptions`、`FileLogOptions`、`UserLogFeed` 和 `ShutdownAsync`，支持明确的进程生命周期管理。
- 😄[新增]-普通日志支持独立 `userMessage`；文件保留技术消息和完整异常堆栈，控制台、`LogView` 与通知只显示用户内容。
- 😄[新增]-`LogView` 支持强类型 `MinimumLevel` / `MaximumLevel` 范围过滤，多个视图可独立显示和清空。
- 💥[重构]-通知能力从 `LogView` 移至 `TopLevel` 附加属性 `LogNotifications`，支持独立级别范围且不回放历史日志。
- 🔨[优化]-文件日志改用持久流、定时 Flush、按日期和大小轮转；有界队列满时等待，不再静默丢弃。
- 🐛[修复]-修复关闭期间写入与 Flush 竞争、通知宿主重新挂载后不恢复订阅、打开日志目录失败后二次抛异常等边界问题。
- 🔨[优化]-重写 Console/Avalonia Demo，覆盖友好异常、文件专用日志、并发、轮转、多视图过滤和独立通知。
- 🔨[优化]-Avalonia Demo 收敛为四个操作按钮和三个日志视图，移除运行配置、指标和动态视图等非必要面板。

## 12.1.0.7 (2026-07-22)

- 🔨[优化]-桌面日志通知窗口更名为 `NotificationWindow`，并按 Notifications、Views、ViewModels、Behaviors、Styles 和 Platform 重新组织源码。
- 🔨[优化]-窗口后台收敛为初始化及对外调用入口；日志队列、倒计时和命令迁入 ViewModel，窗口交互、屏幕定位、级别样式和提醒动效拆分为独立 Behavior。
- 🔨[优化]-倒计时仅在显示秒数变化时更新绑定，窗口定位请求合并调度；继续复用单个倒计时定时器和单个动画定时器，减少 UI 线程分配与重复工作。

## 12.1.0.6 (2026-07-22)

- 🔨[优化]-桌面重要日志提醒动效重构为独立 `Behavior<Border>`，窗口只负责触发提醒，Behavior 统一管理 Transform、冷却、动画状态和卸载清理。
- 🔨[优化]-动效使用单个复用的 `DispatcherTimer` 和静态关键帧，移除逐次创建的异步任务及取消令牌，仅在短时动画期间占用 UI 调度。

## 12.1.0.5 (2026-07-22)

- 😄[新增]-桌面重要日志窗口支持可配置提醒动效：Warn 使用图标脉冲，Error/Fatal 使用分级横向微抖和图标脉冲，也可切换为仅脉冲或完全关闭。
- 🔨[优化]-提醒动效按日志批次最高级别合并并增加 2 秒冷却；鼠标交互或手动翻页时不移动内容，Fatal 升级仍可立即提醒。
- 🔨[优化]-桌面通知窗口改为在最小、最大高度范围内根据日志内容自适应高度，短日志减少无效留白，长日志继续使用滚动区域。
- 😄[新增]-桌面重要日志窗口增加【打开日志目录】入口和 `Ctrl+O` 快捷键，打开失败时在当前按钮反馈，避免错误日志再次触发通知。

## 12.1.0.4 (2026-07-16)

- 🐛[修复]-桌面日志通知窗体默认高度由 330 增加到 430、最大高度调整为 580，正文滚动区域最大高度由 100 增加到 200，改善较长日志内容的可读空间。
- 🔨[优化]-窗体最小/最大高度和正文最大高度改用公开 DynamicResource，并在 `LogNotificationResourceKeys` 中提供常量，方便调用端按界面场景覆盖。

## 12.1.0.3 (2026-07-16)

- 😄[新增]-重要日志通知模式调整为 `None`、`InApp`、`DesktopWindow`，桌面模式提供右下角无边框窗口、倒计时、渐隐、Hover 暂停和键盘操作。
- 😄[新增]-桌面窗口支持最多最近 100 条日志、初始批次顺序轮播、实时日志跳转最新、上一条/下一条导航和正文自定义模板。
- 🔨[优化]-默认重要日志级别为 Error、默认显示时间为 10 秒，未配置应用名称时使用当前进程名，最简用法仅需设置 `NotificationMode="DesktopWindow"`。
- 🔨[优化]-桌面窗口视觉资源统一使用公开 DynamicResource Key，调用方可在 `Application.Resources` 中覆盖，无需 Semi、Ursa 或独立 Theme 包。

## 12.1.0.2 (2026-07-16)

- 😄[新增]-`CodeWF.LogViewer.Avalonia` 支持按最低日志级别弹出重要日志 Notification，可配置总开关、显示时间和 TopLevel Host，并支持 AXAML 与后台代码配置。
- 😄[新增]-重要日志通知独立于 UI 日志通道，`log2UI=false` 的日志仍可触发弹出提醒。
- 🔨[优化]-使用 Avalonia `WindowNotificationManager` 接入 Semi.Avalonia Notification 样式，限制同一 Host 最多显示 3 条通知，并同步更新 Demo 与使用文档。

## 12.0.5.5 (2026-06-25)

- 🐛[修复]-`CodeWF.LogViewer.Avalonia` 日志文本默认按字号生成更舒适的行高，并开放 `LogLineHeightMultiplier` 便于调用方按场景微调。

## 12.0.5.4 (2026-06-25)

- 🐛[修复]-`CodeWF.LogViewer.Avalonia` 禁用日志视图横向滚动条，日志文本按控件可视宽度自动换行，窗口或布局分隔条调整后可随宽度重新排版。

## 12.0.5.3 (2026-06-24)

- 😄[新增]-`CodeWF.Log.Core` 新增 `Logger.Warn(string content, Exception? ex, ...)` 重载，警告日志可记录异常堆栈信息。

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
