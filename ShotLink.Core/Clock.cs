namespace ShotLink.Core;

/// <summary>今の時刻。同じ中身の除外と開く回数の上限を、試験で時計を進めて確かめるために差し替えられるようにしておく</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset Now => DateTimeOffset.Now;
}
