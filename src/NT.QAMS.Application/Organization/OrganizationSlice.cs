using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Platform;
using NT.QAMS.Domain.Organization;
using NT.QAMS.SharedKernel.Localization;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Organization;

// ── Branches & departments ───────────────────────────────────────────────────

public sealed record CreateBranchCommand(string Code, string Name, string? City) : ICommand<Guid>;

public sealed class CreateBranchValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateBranchHandler(IAppDbContext db) : ICommandHandler<CreateBranchCommand, Guid>
{
    public async Task<Guid> Handle(CreateBranchCommand c, CancellationToken ct)
    {
        var code = c.Code.Trim().ToUpperInvariant();
        if (await db.Branches.AnyAsync(b => b.Code == code, ct))
        {
            throw new DomainException("ORG-010", $"Branch code '{code}' already exists.");
        }

        var branch = Branch.Create(code, c.Name, c.City);
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);
        return branch.Id;
    }
}

public sealed record CreateDepartmentCommand(Guid BranchId, string Code, string Name) : ICommand<Guid>;

public sealed class CreateDepartmentHandler(IAppDbContext db) : ICommandHandler<CreateDepartmentCommand, Guid>
{
    public async Task<Guid> Handle(CreateDepartmentCommand c, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == c.BranchId && b.IsActive, ct))
        {
            throw new DomainException("ORG-011", "Branch not found or inactive.");
        }

        var code = c.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.BranchId == c.BranchId && d.Code == code, ct))
        {
            throw new DomainException("ORG-012", $"Department code '{code}' already exists in this branch.");
        }

        var department = Department.Create(c.BranchId, code, c.Name);
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return department.Id;
    }
}

public sealed record DeactivateOrgUnitCommand(Guid Id, bool IsBranch) : ICommand;

public sealed class DeactivateOrgUnitHandler(IAppDbContext db) : ICommandHandler<DeactivateOrgUnitCommand>
{
    public async Task Handle(DeactivateOrgUnitCommand c, CancellationToken ct)
    {
        if (c.IsBranch)
        {
            var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == c.Id, ct)
                ?? throw new DomainException("ORG-404", "Branch not found.");
            branch.Deactivate();
        }
        else
        {
            var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == c.Id, ct)
                ?? throw new DomainException("ORG-404", "Department not found.");
            department.Deactivate();
        }

        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetOrgTreeQuery : IQuery<IReadOnlyList<BranchDto>>;

public sealed class GetOrgTreeHandler(IAppDbContext db)
    : IQueryHandler<GetOrgTreeQuery, IReadOnlyList<BranchDto>>
{
    public async Task<IReadOnlyList<BranchDto>> Handle(GetOrgTreeQuery q, CancellationToken ct) =>
        await db.Branches.AsNoTracking().OrderBy(b => b.Code)
            .Select(b => new BranchDto(b.Id, b.Code, b.Name, b.City, b.IsActive))
            .ToListAsync(ct);
}

public sealed record GetDepartmentsQuery(Guid? BranchId = null) : IQuery<IReadOnlyList<DepartmentDto>>;

public sealed class GetDepartmentsHandler(IAppDbContext db)
    : IQueryHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(GetDepartmentsQuery q, CancellationToken ct)
    {
        var query = db.Departments.AsNoTracking();
        if (q.BranchId is { } branchId)
        {
            query = query.Where(d => d.BranchId == branchId);
        }

        return await query.OrderBy(d => d.Code)
            .Select(d => new DepartmentDto(d.Id, d.BranchId, d.Code, d.Name, d.IsActive))
            .ToListAsync(ct);
    }
}

// ── Test catalog & LOVs ──────────────────────────────────────────────────────

public sealed record CreateTestCommand(string TestCode, string TestName, string Methodology, int TurnaroundHours)
    : ICommand<Guid>;

public sealed class CreateTestHandler(IAppDbContext db) : ICommandHandler<CreateTestCommand, Guid>
{
    public async Task<Guid> Handle(CreateTestCommand c, CancellationToken ct)
    {
        var code = c.TestCode.Trim().ToUpperInvariant();
        if (await db.TestCatalogItems.AnyAsync(t => t.TestCode == code, ct))
        {
            throw new DomainException("ORG-013", $"Test code '{code}' already exists.");
        }

        var item = TestCatalogItem.Create(code, c.TestName, c.Methodology, c.TurnaroundHours);
        db.TestCatalogItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }
}

public sealed record GetTestCatalogQuery : IQuery<IReadOnlyList<TestCatalogDto>>;

public sealed class GetTestCatalogHandler(IAppDbContext db)
    : IQueryHandler<GetTestCatalogQuery, IReadOnlyList<TestCatalogDto>>
{
    public async Task<IReadOnlyList<TestCatalogDto>> Handle(GetTestCatalogQuery q, CancellationToken ct) =>
        await db.TestCatalogItems.AsNoTracking().OrderBy(t => t.TestCode)
            .Select(t => new TestCatalogDto(
                t.Id, t.TestCode, t.TestName, t.Methodology, t.TurnaroundHours, t.IsActive))
            .ToListAsync(ct);
}

public sealed record UpsertLovCommand(
    string Category, string Code, string NameEn, string? NameAr, string? NameFr, int SortOrder)
    : ICommand<Guid>;

public sealed class UpsertLovValidator : AbstractValidator<UpsertLovCommand>
{
    public UpsertLovValidator()
    {
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpsertLovHandler(IAppDbContext db) : ICommandHandler<UpsertLovCommand, Guid>
{
    public async Task<Guid> Handle(UpsertLovCommand c, CancellationToken ct)
    {
        var category = c.Category.Trim().ToUpperInvariant();
        var code = c.Code.Trim().ToUpperInvariant();
        var name = LocalizedText.Create(c.NameEn, c.NameAr, c.NameFr);

        var existing = await db.LovEntries
            .SingleOrDefaultAsync(l => l.Category == category && l.Code == code, ct);

        if (existing is not null)
        {
            existing.Update(name, c.SortOrder);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var entry = LovEntry.Create(category, code, name, c.SortOrder);
        db.LovEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }
}

public sealed record GetLovsQuery(string? Category = null) : IQuery<IReadOnlyList<LovDto>>;

public sealed class GetLovsHandler(IAppDbContext db) : IQueryHandler<GetLovsQuery, IReadOnlyList<LovDto>>
{
    public async Task<IReadOnlyList<LovDto>> Handle(GetLovsQuery q, CancellationToken ct)
    {
        var query = db.LovEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Category))
        {
            var category = q.Category.Trim().ToUpperInvariant();
            query = query.Where(l => l.Category == category);
        }

        return await query.OrderBy(l => l.Category).ThenBy(l => l.SortOrder)
            .Select(l => new LovDto(
                l.Id, l.Category, l.Code, l.Name.En, l.Name.Ar, l.Name.Fr, l.SortOrder, l.IsActive))
            .ToListAsync(ct);
    }
}
