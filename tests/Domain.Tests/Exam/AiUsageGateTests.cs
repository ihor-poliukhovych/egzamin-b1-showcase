using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using B1.Infrastructure.Review;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace B1.Domain.Tests.Exam;

public class AiUsageGateTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    public AiUsageGateTests()
    {
        _connection.Open();
        _services = new ServiceCollection().AddDbContext<AppDbContext>(o => o.UseSqlite(_connection)).BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private AiUsageGate Gate(int dailyLimit = 2, int dialogTurns = 3) =>
        new(_services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new ReviewOptions { DailyLimit = dailyLimit, DailyDialogTurns = dialogTurns }), _clock);

    [Fact]
    public async Task TryReserve_WhenLimitReached_ReturnsFalse_AndKindsAreCountedSeparately()
    {
        var gate = Gate();

        var reviews = new[]
        {
            await gate.TryReserveAsync("student-1", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None),
            await gate.TryReserveAsync("student-1", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None),
            await gate.TryReserveAsync("student-1", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None),
        };
        var dialog = await gate.TryReserveAsync("student-1", AiUsageKind.DialogTurn, Guid.NewGuid(), PartKind.Mowienie, CancellationToken.None);
        var otherUser = await gate.TryReserveAsync("student-2", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None);

        reviews.Should().Equal(true, true, false);
        dialog.Should().BeTrue();
        otherUser.Should().BeTrue();
    }

    [Fact]
    public async Task TryReserve_WhenNewUtcDay_StartsCountingAgain()
    {
        var gate = Gate(dailyLimit: 1);
        (await gate.TryReserveAsync("student-1", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None)).Should().BeTrue();

        _clock.Now = _clock.Now.AddDays(1);

        (await gate.TryReserveAsync("student-1", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task TryReserve_WhenCalledConcurrently_NeverExceedsLimit()
    {
        var gate = Gate(dailyLimit: 3);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            gate.TryReserveAsync("student-1", AiUsageKind.Review, Guid.NewGuid(), PartKind.Pisanie, CancellationToken.None)));

        results.Count(r => r).Should().Be(3);
        (await gate.CountTodayAsync("student-1", AiUsageKind.Review, CancellationToken.None)).Should().Be(3);
    }

    [Fact]
    public void GroupingBackoff_AfterFailure_BlocksForTenMinutes()
    {
        var backoff = new ErrorGroupingBackoff(_clock);

        backoff.Failed("student-1");
        var blocked = backoff.IsBlocked("student-1");
        _clock.Now = _clock.Now.Add(ErrorGroupingBackoff.Delay);

        blocked.Should().BeTrue();
        backoff.IsBlocked("student-1").Should().BeFalse();
        backoff.IsBlocked("student-2").Should().BeFalse();
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
