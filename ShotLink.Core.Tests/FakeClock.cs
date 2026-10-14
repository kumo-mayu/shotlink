using ShotLink.Core;

namespace ShotLink.Core.Tests;

sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(9));

    public void Advance(TimeSpan by) => Now += by;
}
