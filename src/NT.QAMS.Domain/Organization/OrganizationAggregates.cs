using NT.QAMS.SharedKernel.Localization;
using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Organization;

/// <summary>
/// Reference-data aggregates: the published language every other module points
/// at. Deactivation, never deletion — historical records reference these rows.
/// </summary>
public sealed class Branch : AggregateRoot, ITenantScoped
{
    private Branch() { Code = null!; Name = null!; }

    public Guid TenantId { get; set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? City { get; private set; }
    public bool IsActive { get; private set; }

    public static Branch Create(string code, string name, string? city)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ORG-001", "Branch code and name are required.");
        }

        return new Branch
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            IsActive = true,
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ORG-002", "Branch name is required.");
        }

        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}

public sealed class Department : AggregateRoot, ITenantScoped
{
    private Department() { Code = null!; Name = null!; }

    public Guid TenantId { get; set; }
    public Guid BranchId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    public static Department Create(Guid branchId, string code, string name)
    {
        if (branchId == Guid.Empty)
        {
            throw new DomainException("ORG-003", "A department must belong to a branch.");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ORG-004", "Department code and name are required.");
        }

        return new Department
        {
            BranchId = branchId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsActive = true,
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ORG-005", "Department name is required.");
        }

        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}

public sealed class TestCatalogItem : AggregateRoot, ITenantScoped
{
    private TestCatalogItem() { TestCode = null!; TestName = null!; Methodology = null!; }

    public Guid TenantId { get; set; }
    public string TestCode { get; private set; }
    public string TestName { get; private set; }
    public string Methodology { get; private set; }
    public int TurnaroundHours { get; private set; }
    public bool IsActive { get; private set; }

    public static TestCatalogItem Create(string testCode, string testName, string methodology, int turnaroundHours)
    {
        if (string.IsNullOrWhiteSpace(testCode) || string.IsNullOrWhiteSpace(testName))
        {
            throw new DomainException("ORG-006", "Test code and name are required.");
        }

        if (turnaroundHours < 1)
        {
            throw new DomainException("ORG-007", "Turnaround time must be at least one hour.");
        }

        return new TestCatalogItem
        {
            TestCode = testCode.Trim().ToUpperInvariant(),
            TestName = testName.Trim(),
            Methodology = methodology?.Trim() ?? string.Empty,
            TurnaroundHours = turnaroundHours,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>Trilingual list-of-values entry — the LocalizedText shared-kernel VO in action.</summary>
public sealed class LovEntry : AggregateRoot, ITenantScoped
{
    private LovEntry() { Category = null!; Code = null!; Name = null!; }

    public Guid TenantId { get; set; }
    public string Category { get; private set; }
    public string Code { get; private set; }
    public LocalizedText Name { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static LovEntry Create(string category, string code, LocalizedText name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("ORG-008", "LOV category and code are required.");
        }

        return new LovEntry
        {
            Category = category.Trim().ToUpperInvariant(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
        };
    }

    public void Update(LocalizedText name, int sortOrder)
    {
        Name = name;
        SortOrder = sortOrder;
    }

    public void Deactivate() => IsActive = false;
}
