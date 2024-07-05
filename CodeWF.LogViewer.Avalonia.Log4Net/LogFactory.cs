using System;
using System.Threading;

namespace CodeWF.LogViewer.Avalonia.Log4Net
{
    public class LogFactory
    {
        private static readonly Lazy<LogFactory> InstanceOfLogFactory =
            new Lazy<LogFactory>(() => new LogFactory(), LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<ILog> InstanceOfLogManager = new Lazy<ILog>(() => new LogManager(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        private LogFactory()
        {
        }

        public static LogFactory Instance => InstanceOfLogFactory.Value;

        public ILog Log => InstanceOfLogManager.Value;
    }
}