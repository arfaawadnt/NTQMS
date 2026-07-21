using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Platform;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Notifications;

public sealed record UpsertNotificationRuleCommand(
    string EventKey, string RecipientRoles, bool EmailEnabled,
    string SubjectTemplate, string BodyTemplate) : ICommand<Guid>;

public sealed class UpsertNotificationRuleHandler(IAppDbContext db)
    : ICommandHandler<UpsertNotificationRuleCommand, Guid>
{
    public async Task<Guid> Handle(UpsertNotificationRuleCommand c, CancellationToken ct)
    {
        var key = c.EventKey.Trim().ToUpperInvariant();
        var existing = await db.NotificationRules.SingleOrDefaultAsync(r => r.EventKey == key, ct);

        if (existing is not null)
        {
            existing.Update(c.RecipientRoles, c.EmailEnabled, c.SubjectTemplate, c.BodyTemplate);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var rule = NotificationRule.Create(
            key, c.RecipientRoles, c.EmailEnabled, c.SubjectTemplate, c.BodyTemplate);
        db.NotificationRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule.Id;
    }
}

public sealed record GetNotificationRulesQuery : IQuery<IReadOnlyList<NotificationRuleDto>>;

public sealed class GetNotificationRulesHandler(IAppDbContext db)
    : IQueryHandler<GetNotificationRulesQuery, IReadOnlyList<NotificationRuleDto>>
{
    public async Task<IReadOnlyList<NotificationRuleDto>> Handle(
        GetNotificationRulesQuery q, CancellationToken ct) =>
        await db.NotificationRules.AsNoTracking().OrderBy(r => r.EventKey)
            .Select(r => new NotificationRuleDto(
                r.Id, r.EventKey, r.RecipientRoles, r.EmailEnabled,
                r.SubjectTemplate, r.BodyTemplate, r.IsActive))
            .ToListAsync(ct);
}

/// <summary>The signed-in user's in-app notification feed.</summary>
public sealed record GetMyNotificationsQuery(bool UnreadOnly = false)
    : IQuery<IReadOnlyList<NotificationFeedItemDto>>;

public sealed class GetMyNotificationsHandler(IAppDbContext db, ICurrentUser user)
    : IQueryHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationFeedItemDto>>
{
    public async Task<IReadOnlyList<NotificationFeedItemDto>> Handle(
        GetMyNotificationsQuery q, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var query = db.NotificationDispatches.AsNoTracking()
            .Where(d => d.RecipientUserId == userId);

        if (q.UnreadOnly)
        {
            query = query.Where(d => !d.ReadByRecipient);
        }

        return await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(200)
            .Select(d => new NotificationFeedItemDto(
                d.Id, d.EventKey, d.Subject, d.Body, d.ReadByRecipient,
                d.EmailStatus.ToString(), d.CreatedAtUtc))
            .ToListAsync(ct);
    }
}

public sealed record MarkNotificationReadCommand(Guid DispatchId) : ICommand;

public sealed class MarkNotificationReadHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand c, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var dispatch = await db.NotificationDispatches
            .SingleOrDefaultAsync(d => d.Id == c.DispatchId && d.RecipientUserId == userId, ct)
            ?? throw new DomainException("NTF-404", "Notification not found.");
        dispatch.MarkRead();
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Delivery monitor for administrators (email statuses, errors).</summary>
public sealed record GetDispatchMonitorQuery(string? Status = null)
    : IQuery<IReadOnlyList<DispatchMonitorItemDto>>;

public sealed class GetDispatchMonitorHandler(IAppDbContext db)
    : IQueryHandler<GetDispatchMonitorQuery, IReadOnlyList<DispatchMonitorItemDto>>
{
    public async Task<IReadOnlyList<DispatchMonitorItemDto>> Handle(
        GetDispatchMonitorQuery q, CancellationToken ct)
    {
        var query = db.NotificationDispatches.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(d => d.EmailStatus.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(500)
            .Select(d => new DispatchMonitorItemDto(
                d.Id, d.EventKey, d.RecipientUserId, d.RecipientEmail,
                d.Subject, d.EmailStatus.ToString(), d.Error, d.CreatedAtUtc))
            .ToListAsync(ct);
    }
}
