using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace CodeWF.LogViewer.Avalonia.Log4Net
{
    internal class LogManager : ILog, IDisposable
    {
        private const int MaxCount = 500;
        private log4net.ILog _logProxy;
        private readonly BindingList<LogInfo> _logs;

        internal LogManager()
        {
            _logs = new BindingList<LogInfo>();
            var path = Assembly.GetExecutingAssembly().Location;
            path = path.Substring(0, path.LastIndexOf("\\", StringComparison.Ordinal));
            path = Path.Combine(path, "log4net.config");
            _logProxy = Create(path, "logger");
        }

        public Level Level =>
            ((Logger)((LoggerWrapperImpl)_logProxy).Logger).Level;

        public void Dispose()
        {
            _logProxy = null;
            _logs.Clear();
            LogNotifyEvent = null;
        }

        public event Action<LogInfo> LogNotifyEvent;

        public bool GetLogManagerState(LogType logType)
        {
            var result = false;
            switch (logType)
            {
                case LogType.Debug:
                    result = _logProxy.IsDebugEnabled;
                    break;
                case LogType.Info:
                    result = _logProxy.IsInfoEnabled;
                    break;
                case LogType.Warn:
                    result = _logProxy.IsWarnEnabled;
                    break;
                case LogType.Error:
                    result = _logProxy.IsErrorEnabled;
                    break;
                case LogType.Fatal:
                default:
                    result = _logProxy.IsFatalEnabled;
                    break;
            }

            return result;
        }

        public void AddLog(LogType logType, object message)
        {
            RecordLog(logType, message.ToString());
            switch (logType)
            {
                case LogType.Debug:
                    _logProxy.Debug(message);
                    break;
                case LogType.Info:
                    _logProxy.Info(message);
                    break;
                case LogType.Warn:
                    _logProxy.Warn(message);
                    break;
                case LogType.Error:
                    _logProxy.Error(message);
                    break;
                case LogType.Fatal:
                default:
                    _logProxy.Fatal(message);
                    break;
            }
        }

        public void AddLog(LogType logType, object message, Exception exception)
        {
            RecordLog(logType, $"{message}:{exception}");
            switch (logType)
            {
                case LogType.Debug:
                    _logProxy.Debug(message, exception);
                    break;
                case LogType.Info:
                    _logProxy.Info(message, exception);
                    break;
                case LogType.Warn:
                    _logProxy.Warn(message, exception);
                    break;
                case LogType.Error:
                    _logProxy.Error(message, exception);
                    break;
                case LogType.Fatal:
                default:
                    _logProxy.Fatal(message, exception);
                    break;
            }
        }

        public void Debug(object message)
        {
            AddLog(LogType.Debug, message);
        }

        public void Info(object message)
        {
            AddLog(LogType.Info, message);
        }

        public void Warn(object message)
        {
            AddLog(LogType.Warn, message);
        }

        public void Warn(object message, Exception exception)
        {
            AddLog(LogType.Warn, message, exception);
        }

        public void Error(object message)
        {
            AddLog(LogType.Error, message);
        }

        public void Error(object message, Exception exception)
        {
            AddLog(LogType.Error, message, exception);
        }

        public void Fatal(object message)
        {
            AddLog(LogType.Fatal, message);
        }

        public void Fatal(object message, Exception exception)
        {
            AddLog(LogType.Fatal, message, exception);
        }

        public void AddFormatLog(LogType logType, string format, params object[] args)
        {
            RecordLog(logType, string.Format(format, args));
            switch (logType)
            {
                case LogType.Debug:
                    _logProxy.DebugFormat(format, args);
                    break;
                case LogType.Info:
                    _logProxy.InfoFormat(format, args);
                    break;
                case LogType.Warn:
                    _logProxy.WarnFormat(format, args);
                    break;
                case LogType.Error:
                    _logProxy.ErrorFormat(format, args);
                    break;
                case LogType.Fatal:
                default:
                    _logProxy.FatalFormat(format, args);
                    break;
            }
        }

        public void AddFormatLog(LogType logType, IFormatProvider provider, string format, params object[] args)
        {
            RecordLog(logType, string.Format(provider, format, args));
            switch (logType)
            {
                case LogType.Debug:
                    _logProxy.DebugFormat(provider, format, args);
                    break;
                case LogType.Info:
                    _logProxy.InfoFormat(provider, format, args);
                    break;
                case LogType.Warn:
                    _logProxy.WarnFormat(provider, format, args);
                    break;
                case LogType.Error:
                    _logProxy.ErrorFormat(provider, format, args);
                    break;
                case LogType.Fatal:
                default:
                    _logProxy.FatalFormat(provider, format, args);
                    break;
            }
        }

        public BindingList<LogInfo> GetLogList()
        {
            return _logs;
        }

        public void Clear()
        {
            _logs.Clear();
        }

        public string GetLogFilesDirectory()
        {
            try
            {
                var appenders =
                    ((Logger)((LoggerWrapperImpl)_logProxy).Logger).Appenders;
                foreach (var appender in appenders)
                    if (appender is FileAppender fileAppender)
                        return Path.GetDirectoryName(fileAppender.File);
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static log4net.ILog Create(params string[] args)
        {
            var configFileName = "log4net.config";
            var appenderName = "FileLogger";
            if (args != null && args.Length >= 1) configFileName = args[0];

            if (args != null && args.Length >= 2) appenderName = args[1];

            if (!File.Exists(configFileName)) throw new FileNotFoundException("日志配置文件不存在", configFileName);

            XmlConfigurator.ConfigureAndWatch(new FileInfo(configFileName));
            var log = log4net.LogManager.Exists(appenderName);
            if (log == null) throw new ArgumentException($"log4net 日志记录器不存在：{appenderName}, args[1]");

            return log;
        }

        private void RecordLog(LogType logType, string msg)
        {
            var logInfo = new LogInfo(logType, msg);

            try
            {
                _logs.Insert(0, logInfo);
                if (_logs.Count > MaxCount) _logs.RemoveAt(_logs.Count - 1);
            }
            catch
            {
                // ignored
            }

            LogNotifyEvent?.Invoke(logInfo);
        }
    }
}