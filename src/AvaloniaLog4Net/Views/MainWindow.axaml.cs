using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CodeWF.LogViewer.Avalonia.Log4Net;
using System;

namespace AvaloniaLog4Net.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AddDebugLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Debug(
                $"模块名称A - {Random.Shared.Next(1, 11)}号事件，处理耗时{Random.Shared.Next(1, 1000)}毫秒");
        }

        private void AddInfoLog_OnClick(object? sender, RoutedEventArgs e)
        {
            string GetRandomAction() => new[] { "登录", "搜索", "下单" }.RandomChoice();
            LogFactory.Instance.Log.Info($"服务B - 用户ID：{Guid.NewGuid()}，执行操作：{GetRandomAction()}");
        }

        private void AddWarnLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Warn($"数据库 - 查询[表名]时，发现重复记录ID: {Random.Shared.Next(10000, 99999)}");
        }

        private void AddErrorLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Error($"[接口调用] - 调用[接口名]时出错，异常信息：{new Exception("随机错误").Message}");
        }

        private void AddFatalLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Fatal(Random.Shared.Next(0, 10) == 0
                ? $"致命：[程序] - 系统崩溃，错误代码：{Random.Shared.Next(1000, 9999)}"
                : $"致命：[程序] - 发现严重问题，已停止服务。");
        }
    }

    public static class RandomExt
    {
        public static T RandomChoice<T>(this T[] array) => array[Random.Shared.Next(array.Length)];
    }
}