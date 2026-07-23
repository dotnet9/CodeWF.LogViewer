using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace CodeWF.Log.Extensions.Logging;

public static class CodeWFLoggingBuilderExtensions
{
    public static ILoggingBuilder AddCodeWF(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddConfiguration();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, CodeWFLoggerProvider>());
        LoggerProviderOptions.RegisterProviderOptions<CodeWFLoggerOptions, CodeWFLoggerProvider>(builder.Services);
        return builder;
    }

    public static ILoggingBuilder AddCodeWF(this ILoggingBuilder builder, Action<CodeWFLoggerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddCodeWF();
        builder.Services.Configure(configure);
        return builder;
    }
}
