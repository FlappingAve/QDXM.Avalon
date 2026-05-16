namespace QDXM.Avalon.Services;

public sealed record AppLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Source,
    string Message);
