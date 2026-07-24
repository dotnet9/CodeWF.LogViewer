using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using var services = new ServiceCollection()
    .AddLogging(builder => builder.AddCodeWF(options =>
    {
        options.BridgeStaticLogger = false;
        options.File.Enabled = false;
        options.Console.Enabled = false;
        options.LineTemplate = "{Timestamp:O} [{Level:u3}] {UserMessage}{NewLine}";
    }))
    .BuildServiceProvider();

var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AotSmoke");
logger.LogInformation("Native AOT diagnostic event {Value}", 42);
logger.LogUserWarning("Native AOT 用户消息。", "Native AOT user event {Value}", 43);
var feed = services.GetRequiredService<LogEventFeed>();
for (var retry = 0; retry < 100 && feed.GetRecentEvents().Count < 2; retry++)
    await Task.Delay(10);
if (feed.GetRecentEvents().Count != 2) return 1;
return 0;
