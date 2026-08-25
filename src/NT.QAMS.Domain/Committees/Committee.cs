using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Committees;

/// <summary>How often a committee is required to meet.</summary>
public enum CommitteeFrequency { Weekly, Monthly, Quarterly, Biannual, Annual, AdHoc }

/// <summary>Lifecycle of a committee.</summary>
public enum CommitteeStatus { Active, Disbanded }

/// <summary>A member of a committee, with the role they hold on it.</summary>
public sealed class CommitteeMember : Entity
{
    internal CommitteeMember(Guid userId, string roleTitle)
    {
        UserId = userId;
        RoleTitle = roleTitle;
    }

    private CommitteeMember() { RoleTitle = null!; }

    public Guid UserId { get; private set; }

    /// <summary>The member's role on the committee (Chair, Secretary, Member, …).</summary>
    public string RoleTitle { get; private set; }
}

/// <summary>
/// A governance committee (HQMS M17): its terms of reference, required meeting frequency,
/// quorum rule and membership. Accreditation requires evidence that quality governance
/// actually meets, decides and follows through; this register is the standing definition
/// against which meetings (a separate aggregate) are held.
/// </summary>
public sealed class Committee : AggregateRoot, ITenantScoped
{
    private readonly List<CommitteeMember> _members = [];

    private Committee()
    {
        Name = null!;
        TermsOfReference = null!;
    }

    public Guid TenantId { get; set; }
    public string Name { get; private set; }
    public string TermsOfReference { get; private set; }
    public CommitteeFrequency Frequency { get; private set; }

    /// <summary>Minimum members present for a meeting to be quorate.</summary>
    public int QuorumSize { get; private set; }

    public CommitteeStatus Status { get; private set; }

    public IReadOnlyList<CommitteeMember> Members => _members.AsReadOnly();

    public static Committee Create(string name, string termsOfReference, CommitteeFrequency frequency, int quorumSize)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("CMT-001", "A committee name is required.");
        }

        if (string.IsNullOrWhiteSpace(termsOfReference))
        {
            throw new DomainException("CMT-002", "Terms of reference are required.");
        }

        if (quorumSize < 1)
        {
            throw new DomainException("CMT-003", "Quorum must be at least 1.");
        }

        return new Committee
        {
            Name = name.Trim(),
            TermsOfReference = termsOfReference.Trim(),
            Frequency = frequency,
            QuorumSize = quorumSize,
            Status = CommitteeStatus.Active,
        };
    }

    public Guid AddMember(Guid userId, string roleTitle)
    {
        RequireActive();
        if (userId == Guid.Empty)
        {
            throw new DomainException("CMT-010", "A member user is required.");
        }

        if (string.IsNullOrWhiteSpace(roleTitle))
        {
            throw new DomainException("CMT-011", "A member role is required.");
        }

        if (_members.Any(m => m.UserId == userId))
        {
            throw new DomainException("CMT-012", "That user is already a member of this committee.");
        }

        var member = new CommitteeMember(userId, roleTitle.Trim());
        _members.Add(member);
        return member.Id;
    }

    public void RemoveMember(Guid memberId)
    {
        RequireActive();
        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new DomainException("CMT-013", "Committee member not found.");
        _members.Remove(member);
    }

    public void UpdateQuorum(int quorumSize)
    {
        RequireActive();
        if (quorumSize < 1)
        {
            throw new DomainException("CMT-003", "Quorum must be at least 1.");
        }

        QuorumSize = quorumSize;
    }

    public void Disband()
    {
        if (Status == CommitteeStatus.Disbanded)
        {
            throw new InvalidStateTransitionException("CMT-014", "The committee is already disbanded.");
        }

        Status = CommitteeStatus.Disbanded;
    }

    private void RequireActive()
    {
        if (Status != CommitteeStatus.Active)
        {
            throw new InvalidStateTransitionException("CMT-015", "A disbanded committee cannot be modified.");
        }
    }
}
