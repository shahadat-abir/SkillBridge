namespace SkillBridge.ViewModels;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public static class TimeDisplay
{
    /// <summary>Stored values are UTC; display in the server's local time zone (Asia/Dhaka on the demo PC).</summary>
    public static string Local(this DateTime utc, string format = "dd MMM yyyy, h:mm tt") =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString(format);

    public static string Local(this DateTime? utc, string format = "dd MMM yyyy, h:mm tt") =>
        utc.HasValue ? utc.Value.Local(format) : "—";

    /// <summary>Converts a value typed in a datetime-local input (server local time) to UTC.</summary>
    public static DateTime LocalInputToUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    public static DateTime UtcToLocalInput(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
}
