using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Platform;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Notifications;

namespace NT.QAMS.Application.Notifications;

/// <summary>
/// Reads the tenant's mail sender identity for the Mail Management page. Returns a
/// not-yet-configured default (with mail enabled) when no row exists, so the page
/// renders sensibly on first visit and the dispatcher's fallback behaviour is
/// mirrored in the UI.
/// </summary>
public sealed record GetMailSettingsQuery : IQuery<MailSettingsDto>;

public sealed class GetMailSettingsHandler(IAppDbContext db)
    : IQueryHandler<GetMailSettingsQuery, MailSettingsDto>
{
    public async Task<MailSettingsDto> Handle(GetMailSettingsQuery q, CancellationToken ct)
    {
        var m = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return m is null
            ? new MailSettingsDto("", "", null, Enabled: true, null, null, Configured: false)
            : new MailSettingsDto(m.FromName, m.FromAddress, m.ReplyTo, m.Enabled, m.BrandColor, m.FooterNote, Configured: true);
    }
}

/// <summary>
/// Sets (or updates) the tenant's mail sender identity and branding. Manage-gated:
/// the sender a tenant's notifications go out as is an administrative configuration.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Notifications, PermissionAction.Manage)]
public sealed record UpsertMailSettingsCommand(
    string FromName, string FromAddress, string? ReplyTo, bool Enabled,
    string? BrandColor, string? FooterNote) : ICommand;

public sealed class UpsertMailSettingsValidator : AbstractValidator<UpsertMailSettingsCommand>
{
    public UpsertMailSettingsValidator()
    {
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FromAddress).NotEmpty().MaximumLength(320);
        RuleFor(x => x.ReplyTo).MaximumLength(320);
        RuleFor(x => x.BrandColor).MaximumLength(9);
        RuleFor(x => x.FooterNote).MaximumLength(500);
    }
}

public sealed class UpsertMailSettingsHandler(IAppDbContext db) : ICommandHandler<UpsertMailSettingsCommand>
{
    public async Task Handle(UpsertMailSettingsCommand c, CancellationToken ct)
    {
        var existing = await db.MailSettings.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            db.MailSettings.Add(TenantMailSettings.Create(
                c.FromName, c.FromAddress, c.ReplyTo, c.Enabled, c.BrandColor, c.FooterNote));
        }
        else
        {
            existing.Update(c.FromName, c.FromAddress, c.ReplyTo, c.Enabled, c.BrandColor, c.FooterNote);
        }

        await db.SaveChangesAsync(ct);
    }
}
