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
                $"ģ������A - {Random.Shared.Next(1, 11)}���¼��������ʱ{Random.Shared.Next(1, 1000)}����");
        }

        private void AddInfoLog_OnClick(object? sender, RoutedEventArgs e)
        {
            string GetRandomAction() => new[] { "��¼", "����", "�µ�" }.RandomChoice();
            LogFactory.Instance.Log.Info($"����B - �û�ID��{Guid.NewGuid()}��ִ�в�����{GetRandomAction()}");
        }

        private void AddWarnLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Warn($"���ݿ� - ��ѯ[����]ʱ�������ظ���¼ID: {Random.Shared.Next(10000, 99999)}");
        }

        private void AddErrorLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Error($"[�ӿڵ���] - ����[�ӿ���]ʱ�����쳣��Ϣ��{new Exception("�������").Message}");
        }

        private void AddFatalLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Fatal(Random.Shared.Next(0, 10) == 0
                ? $"������[����] - ϵͳ������������룺{Random.Shared.Next(1000, 9999)}"
                : $"������[����] - �����������⣬��ֹͣ����");
        }
    }

    public static class RandomExt
    {
        public static T RandomChoice<T>(this T[] array) => array[Random.Shared.Next(array.Length)];
    }
}