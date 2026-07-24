using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Extensions.Logging;

public static class CodeWFLoggerExtensions
{
    public static void LogUser(
        this ILogger logger,
        LogLevel level,
        string userMessage,
        string messageTemplate,
        params object?[] args)
    {
        logger.LogUser(level, default, null, userMessage, messageTemplate, args);
    }

    public static void LogUser(
        this ILogger logger,
        LogLevel level,
        EventId eventId,
        Exception? exception,
        string userMessage,
        string messageTemplate,
        params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTemplate);

        if (!logger.IsEnabled(level)) return;

        var state = new CodeWFUserLogState(userMessage, messageTemplate, args);
        logger.Log(
            level,
            eventId,
            state,
            exception,
            static (logState, _) => logState.ToString());
    }

    public static void LogUserInformation(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args)
    {
        logger.LogUser(LogLevel.Information, default, null, userMessage, messageTemplate, args);
    }

    public static void LogUserTrace(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args) =>
        logger.LogUser(LogLevel.Trace, default, null, userMessage, messageTemplate, args);

    public static void LogUserDebug(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args) =>
        logger.LogUser(LogLevel.Debug, default, null, userMessage, messageTemplate, args);

    public static void LogUserWarning(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args)
    {
        logger.LogUser(LogLevel.Warning, default, null, userMessage, messageTemplate, args);
    }

    public static void LogUserError(
        this ILogger logger,
        Exception? exception,
        string userMessage,
        string messageTemplate,
        params object?[] args)
    {
        logger.LogUser(LogLevel.Error, default, exception, userMessage, messageTemplate, args);
    }

    public static void LogUserCritical(
        this ILogger logger,
        Exception? exception,
        string userMessage,
        string messageTemplate,
        params object?[] args)
    {
        logger.LogUser(LogLevel.Critical, default, exception, userMessage, messageTemplate, args);
    }
}
