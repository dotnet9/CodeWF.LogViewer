# 更新日志（Known）

## V11.3.15（2026-04-27）

- 😄[新增]-添加 CHANGELOG.md 更新日志文件，从 2026-04-27 开始记录版本变更
- 😄[新增]-重做 AvaloniaLogDemo 为专业日志观察台示例，包含业务场景、输出通道、采样级别、实时流量、批量写入和运行指标
- 🔨[优化]-CodeWF.Log.Core 文件日志后台消费改为批次快照刷新，避免防抖任务与消费线程共享集合
- 🔨[优化]-CodeWF.LogViewer.Avalonia 日志 UI 刷新改为批次快照渲染，提升高频日志场景稳定性
- 🔨[优化]-LogView 控件查找和可空性处理，减少运行时空引用风险并保持构建无警告
- 🐛[修复]-修复 LogInfo 未传 uiContent 时 FriendlyDescription 为空导致 UI 友好文本丢失问题
- 🐛[修复]-修复 Logger.FlushAsync 退出时可能未等待后台批次落盘问题
- 🐛[修复]-修复 EnumExtensions 获取未知枚举字段描述时可能出现空引用的问题
