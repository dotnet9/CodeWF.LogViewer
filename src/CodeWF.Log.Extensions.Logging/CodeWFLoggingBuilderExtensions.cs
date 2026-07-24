using CodeWF.Log.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeWF.Log.Extensions.Logging;

public static class CodeWFLoggingBuilderExtensions
{
    public static ILoggingBuilder AddCodeWF(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddConfiguration();
        builder.Services.AddOptions<CodeWFLoggerOptions>()
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<CodeWFLoggerOptions>, CodeWFLoggerOptionsValidator>());
        builder.Services.TryAddSingleton<CodeWFLoggerRuntime>();
        builder.Services.TryAddSingleton(static services => services.GetRequiredService<CodeWFLoggerRuntime>().Events);
        builder.Services.TryAddSingleton(static services => services.GetRequiredService<CodeWFLoggerRuntime>().Health);
        builder.Services.TryAddSingleton<ILineTemplateController>(static services =>
            services.GetRequiredService<CodeWFLoggerRuntime>().LineTemplate);
        builder.Services.TryAddSingleton<IFileOutputTemplateController>(static services =>
            services.GetRequiredService<CodeWFLoggerRuntime>().FileOutputTemplate);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, CodeWFLoggerProvider>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<CodeWFLoggerOptions>, CodeWFLoggerOptionsSetup>());
        return builder;
    }

    public static ILoggingBuilder AddCodeWF(this ILoggingBuilder builder, Action<CodeWFLoggerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        builder.AddCodeWF();
        builder.Services.Configure(configure);
        return builder;
    }

}
