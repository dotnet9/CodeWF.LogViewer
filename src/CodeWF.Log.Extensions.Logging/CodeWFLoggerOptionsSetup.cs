using CodeWF.Log.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CodeWF.Log.Extensions.Logging;

/// <summary>
/// AOT-safe binder for Logging:CodeWF. Pipeline options are intentionally read once when the provider starts.
/// </summary>
internal sealed class CodeWFLoggerOptionsSetup(IEnumerable<IConfiguration> configurations)
    : IConfigureOptions<CodeWFLoggerOptions>
{
    private readonly IConfiguration? _configuration = configurations.LastOrDefault();

    public void Configure(CodeWFLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_configuration is null) return;

        const string root = "Logging:CodeWF";
        SetString($"{root}:LineTemplate", value => options.LineTemplate = value);
        SetBool($"{root}:BridgeStaticLogger", value => options.BridgeStaticLogger = value);

        SetBool($"{root}:File:Enabled", value => options.File.Enabled = value);
        SetEnum<LogLevel>($"{root}:File:MinimumLevel", value => options.File.MinimumLevel = value);
        SetString($"{root}:File:DirectoryPath", value => options.File.DirectoryPath = value);
        SetLong($"{root}:File:MaxFileSizeBytes", value => options.File.MaxFileSizeBytes = value);
        SetInt($"{root}:File:RetentionDays", value => options.File.RetentionDays = value);
        SetNullableInt($"{root}:File:RetainedFileCountLimit", value => options.File.RetainedFileCountLimit = value);
        SetNullableLong($"{root}:File:MaxDirectorySizeBytes", value => options.File.MaxDirectorySizeBytes = value);
        SetInt($"{root}:File:BatchSize", value => options.File.BatchSize = value);
        SetTimeSpan($"{root}:File:FlushInterval", value => options.File.FlushInterval = value);
        SetString($"{root}:File:TimestampFormat", value => options.File.TimestampFormat = value);
        SetNullableString($"{root}:File:OutputTemplate", value => options.File.OutputTemplate = value);

        SetBool($"{root}:Console:Enabled", value => options.Console.Enabled = value);
        SetEnum<LogLevel>($"{root}:Console:MinimumLevel", value => options.Console.MinimumLevel = value);
        SetString($"{root}:Console:TimestampFormat", value => options.Console.TimestampFormat = value);

        SetBool($"{root}:EventFeed:Enabled", value => options.EventFeed.Enabled = value);
        SetInt($"{root}:EventFeed:RecentCapacity", value => options.EventFeed.RecentCapacity = value);

        SetBool($"{root}:Capture:Scopes", value => options.Capture.Scopes = value);
        SetBool($"{root}:Capture:Activity", value => options.Capture.Activity = value);
        SetBool($"{root}:Capture:ActivityTags", value => options.Capture.ActivityTags = value);
        SetBool($"{root}:Capture:ActivityBaggage", value => options.Capture.ActivityBaggage = value);

        SetInt($"{root}:Queue:Capacity", value => options.Queue.Capacity = value);
        SetEnum<LogQueueFullMode>($"{root}:Queue:FullMode", value => options.Queue.FullMode = value);
        SetTimeSpan($"{root}:Queue:EnqueueTimeout", value => options.Queue.EnqueueTimeout = value);
    }

    private string? Read(string key) => _configuration?[key];

    private void SetString(string key, Action<string> assign)
    {
        if (Read(key) is { } value) assign(value);
    }

    private void SetNullableString(string key, Action<string?> assign)
    {
        if (Read(key) is { } value) assign(string.IsNullOrWhiteSpace(value) ? null : value);
    }

    private void SetBool(string key, Action<bool> assign) => SetParsed(
        key,
        bool.TryParse,
        assign,
        "true 或 false");

    private void SetInt(string key, Action<int> assign) => SetParsed(
        key,
        (string value, out int result) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result),
        assign,
        "整数");

    private void SetLong(string key, Action<long> assign) => SetParsed(
        key,
        (string value, out long result) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result),
        assign,
        "整数");

    private void SetNullableInt(string key, Action<int?> assign)
    {
        var value = Read(key);
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(value)) { assign(null); return; }
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) { assign(parsed); return; }
        throw InvalidValue(key, value, "整数或空值");
    }

    private void SetNullableLong(string key, Action<long?> assign)
    {
        var value = Read(key);
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(value)) { assign(null); return; }
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) { assign(parsed); return; }
        throw InvalidValue(key, value, "整数或空值");
    }

    private void SetTimeSpan(string key, Action<TimeSpan> assign) => SetParsed(
        key,
        (string value, out TimeSpan result) => TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out result),
        assign,
        "TimeSpan（例如 00:00:00.100）");

    private void SetEnum<TEnum>(string key, Action<TEnum> assign) where TEnum : struct, Enum
    {
        var value = Read(key);
        if (value is null) return;
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            assign(parsed);
            return;
        }
        throw InvalidValue(key, value, $"{typeof(TEnum).Name} 枚举值");
    }

    private void SetParsed<T>(string key, TryParse<T> tryParse, Action<T> assign, string expected)
    {
        var value = Read(key);
        if (value is null) return;
        if (tryParse(value, out var parsed)) { assign(parsed); return; }
        throw InvalidValue(key, value, expected);
    }

    private static FormatException InvalidValue(string key, string value, string expected) =>
        new($"配置项 '{key}' 的值 '{value}' 无效，应为{expected}。");

    private delegate bool TryParse<T>(string value, out T result);
}

internal sealed class CodeWFLoggerOptionsValidator : IValidateOptions<CodeWFLoggerOptions>
{
    public ValidateOptionsResult Validate(string? name, CodeWFLoggerOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
