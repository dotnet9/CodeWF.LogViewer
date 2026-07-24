using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

public sealed class CodeWFLogHealth
{
    private readonly long[] _droppedByLevel = new long[7];

    public long DroppedCount
    {
        get
        {
            long total = 0;
            for (var index = 0; index < _droppedByLevel.Length; index++)
                total += Interlocked.Read(ref _droppedByLevel[index]);
            return total;
        }
    }

    public CodeWFLogHealthSnapshot GetSnapshot()
    {
        var counts = new Dictionary<LogLevel, long>();
        foreach (var level in Enum.GetValues<LogLevel>())
        {
            var count = Interlocked.Read(ref _droppedByLevel[ToIndex(level)]);
            if (count > 0) counts[level] = count;
        }
        return new CodeWFLogHealthSnapshot(counts.Values.Sum(), counts);
    }

    internal void RecordDropped(LogLevel level) =>
        Interlocked.Increment(ref _droppedByLevel[ToIndex(level)]);

    private static int ToIndex(LogLevel level) => level is >= LogLevel.Trace and <= LogLevel.None ? (int)level : (int)LogLevel.None;
}

public sealed record CodeWFLogHealthSnapshot(
    long DroppedCount,
    IReadOnlyDictionary<LogLevel, long> DroppedByLevel);
