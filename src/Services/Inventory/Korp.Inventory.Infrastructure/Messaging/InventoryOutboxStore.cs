using System.Data;
using Korp.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Korp.Inventory.Infrastructure.Messaging;

public sealed record OutboxDelivery(Guid Id, Guid LockId, string MessageType, int SchemaVersion,
    string Payload, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAtUtc, int AttemptCount);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxDelivery>> ClaimAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task MarkPublishedAsync(OutboxDelivery delivery, DateTimeOffset now, CancellationToken cancellationToken);
    Task RecordFailureAsync(OutboxDelivery delivery, string failureDescription, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class InventoryOutboxStore(
    IDbContextFactory<InventoryDbContext> factory,
    IOptions<OutboxOptions> options) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxDelivery>> ClaimAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var limit = options.Value.BatchSize;
        var messages = await context.OutboxMessages.FromSqlInterpolated($$"""
            SELECT outbox_messages.*, xmin FROM outbox_messages
            WHERE published_at_utc IS NULL AND next_attempt_at_utc <= {{now}}
              AND (lock_id IS NULL OR locked_until_utc <= {{now}})
            ORDER BY next_attempt_at_utc, occurred_at_utc
            FOR UPDATE SKIP LOCKED LIMIT {{limit}}
            """).ToArrayAsync(cancellationToken);
        if (messages.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        var lockId = Guid.NewGuid();
        var lockedUntil = now.AddSeconds(options.Value.LeaseSeconds);
        foreach (var message in messages) message.AcquireLease(lockId, lockedUntil);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Select(message => new OutboxDelivery(message.Id, lockId, message.MessageType,
            message.SchemaVersion, message.Payload, message.CorrelationId, message.CausationId,
            message.OccurredAtUtc, message.AttemptCount)).ToArray();
    }

    public async Task MarkPublishedAsync(OutboxDelivery delivery, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var message = await context.OutboxMessages.SingleOrDefaultAsync(
            candidate => candidate.Id == delivery.Id && candidate.LockId == delivery.LockId, cancellationToken);
        if (message is null) return;
        message.MarkPublished(now);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(OutboxDelivery delivery, string failureDescription, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var message = await context.OutboxMessages.SingleOrDefaultAsync(
            candidate => candidate.Id == delivery.Id && candidate.LockId == delivery.LockId, cancellationToken);
        if (message is null) return;
        message.RecordFailure(failureDescription, now.Add(OutboxBackoff.ForAttempt(delivery.AttemptCount + 1)));
        await context.SaveChangesAsync(cancellationToken);
    }
}

public static class OutboxBackoff
{
    public static TimeSpan ForAttempt(int attempt) => TimeSpan.FromSeconds(attempt switch
    {
        <= 1 => 1, 2 => 2, 3 => 4, 4 => 8, 5 => 16, _ => 30
    });
}
