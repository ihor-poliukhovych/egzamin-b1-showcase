using System.Collections.Concurrent;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace B1.Infrastructure.Review;

/// <summary>
/// Daily AI limits per user. A call is reserved (a usage row is written and committed) BEFORE the model is called and the row is
/// kept whatever the outcome, so failures, parse errors and cancelled requests still count.
/// <para>
/// Check and insert are atomic per user inside one process: a per-user <see cref="SemaphoreSlim"/> serialises them and the
/// pair runs in a transaction. With several API instances this is not enough; a database lock (for example
/// <c>pg_advisory_xact_lock</c> on a hash of the user id) would be needed inside the same transaction.
/// </para>
/// </summary>
public class AiUsageGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly IServiceScopeFactory _scopes;
    private readonly ReviewOptions _options;
    private readonly TimeProvider _clock;

    public AiUsageGate(IServiceScopeFactory scopes, IOptions<ReviewOptions> options, TimeProvider clock)
    {
        _scopes = scopes;
        _options = options.Value;
        _clock = clock;
    }

    public int DailyLimit(AiUsageKind kind) => kind == AiUsageKind.DialogTurn ? _options.DailyDialogTurns : _options.DailyLimit;

    /// <summary>Reserves one model call for the user; false when today's limit for this kind is already used up.</summary>
    public async Task<bool> TryReserveAsync(string userId, AiUsageKind kind, Guid attemptId, PartKind part, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // own scope and DbContext: the caller's pending changes are not flushed together with the reservation
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var now = _clock.GetUtcNow();
            var used = await CountTodayAsync(db, userId, kind, AiReviewUsage.DayStart(now), ct);
            if (AiReviewUsage.LimitReached(used, DailyLimit(kind))) return false;

            db.AiReviewUsages.Add(AiReviewUsage.Create(userId, kind, attemptId, part, now));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> CountTodayAsync(string userId, AiUsageKind kind, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await CountTodayAsync(db, userId, kind, AiReviewUsage.DayStart(_clock.GetUtcNow()), ct);
    }

    private static async Task<int> CountTodayAsync(AppDbContext db, string userId, AiUsageKind kind, DateTimeOffset dayStart, CancellationToken ct)
    {
        var rows = db.AiReviewUsages.Where(u => u.UserId == userId && u.Kind == kind);
        if (!db.IsSqlite()) return await rows.CountAsync(u => u.At >= dayStart, ct);

        // SQLite cannot compare DateTimeOffset in SQL: filter on the client (local development and tests only)
        return (await rows.Select(u => u.At).ToListAsync(ct)).Count(at => at >= dayStart);
    }
}

/// <summary>After a failed error-grouping call the grouper is not called again for that user for <see cref="Delay"/>.</summary>
public class ErrorGroupingBackoff(TimeProvider clock)
{
    public static readonly TimeSpan Delay = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _blockedUntil = new(StringComparer.Ordinal);

    public bool IsBlocked(string userId) => _blockedUntil.TryGetValue(userId, out var until) && clock.GetUtcNow() < until;

    public void Failed(string userId) => _blockedUntil[userId] = clock.GetUtcNow() + Delay;

    public void Succeeded(string userId) => _blockedUntil.TryRemove(userId, out _);
}
