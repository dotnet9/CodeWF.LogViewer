using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CodeWF.LogViewer.Avalonia;
using System;

namespace AvaloniaLog4Net.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Logger.Level = LogType.Info;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AddDebugLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Debug(
                $"ģ������A - {Random.Shared.Next(1, 11)}���¼��������ʱ{Random.Shared.Next(1, 1000)}����");
        }

        private void AddInfoLog_OnClick(object? sender, RoutedEventArgs e)
        {
            string GetRandomAction() => new[] { "��¼", "����", "�µ�" }.RandomChoice();
            Logger.Info($"����B - �û�ID��{Guid.NewGuid()}��ִ�в�����{GetRandomAction()}");
        }

        private void AddWarnLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Warn($"���ݿ� - ��ѯ[����]ʱ�������ظ���¼ID: {Random.Shared.Next(10000, 99999)}");
        }

        private void AddErrorLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Error($"[�ӿڵ���] - ����[�ӿ���]ʱ�����쳣��Ϣ��{new Exception("�������").Message}");
        }

        private void AddFatalLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Fatal(Random.Shared.Next(0, 10) == 0
                ? $"������[����] - ϵͳ������������룺{Random.Shared.Next(1000, 9999)}"
                : $"������[����] - �����������⣬��ֹͣ����");
        }
    }

    public static class RandomExt
    {
        public static T RandomChoice<T>(this T[] array) => array[Random.Shared.Next(array.Length)];
    }
}