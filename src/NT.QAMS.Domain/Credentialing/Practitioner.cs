using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Credentialing;

/// <summary>Lifecycle of a practitioner's appointment.</summary>
public enum PractitionerStatus { Pending, Credentialed, Suspended }

/// <summary>The kind of credential held.</summary>
public enum CredentialType { MedicalLicence, NursingLicence, BoardCertification, Bls, Acls, Other }

/// <summary>Primary-source verification state of a credential.</summary>
public enum VerificationStatus { Pending, Verified }

/// <summary>Lifecycle of a requested clinical privilege.</summary>
public enum PrivilegeStatus { Requested, Granted, Denied, Expired }

/// <summary>
/// A licence / certification held by a practitioner, with its primary-source verification (PSV).
/// A credential is not trusted until an independent verifier records the source it was checked
/// against (SoD-CRD-001).
/// </summary>
public sealed class LicenceCredential : Entity
{
    internal LicenceCredential(CredentialType type, string identifier, string issuer, DateOnly expiresOn)
    {
        Type = type;
        Identifier = identifier;
        Issuer = issuer;
        ExpiresOn = expiresOn;
        VerificationStatus = VerificationStatus.Pending;
    }

    private LicenceCredential() { Identifier = null!; Issuer = null!; }

    public CredentialType Type { get; private set; }
    public string Identifier { get; private set; }
    public string Issuer { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public VerificationStatus VerificationStatus { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public string? VerificationSource { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public bool IsExpired(DateOnly asOf) => ExpiresOn < asOf;

    internal void Verify(Guid verifierId, string source, DateTimeOffset at)
    {
        VerificationStatus = VerificationStatus.Verified;
        VerifiedBy = verifierId;
        VerificationSource = source;
        VerifiedAtUtc = at;
    }
}

/// <summary>A delineated clinical privilege the practitioner may request and be granted.</summary>
public sealed class Privilege : Entity
{
    internal Privilege(string name)
    {
        Name = name;
        Status = PrivilegeStatus.Requested;
    }

    private Privilege() { Name = null!; }

    public string Name { get; private set; }
    public PrivilegeStatus Status { get; private set; }
    public DateOnly? GrantedUntil { get; private set; }
    public string? DenialReason { get; private set; }

    internal void Grant(DateOnly? grantedUntil) { Status = PrivilegeStatus.Granted; GrantedUntil = grantedUntil; DenialReason = null; }
    internal void Deny(string reason) { Status = PrivilegeStatus.Denied; DenialReason = reason; }

    /// <summary>A granted privilege is active while it is not past its (optional) expiry.</summary>
    public bool IsActive(DateOnly asOf) => Status == PrivilegeStatus.Granted && (GrantedUntil is null || GrantedUntil >= asOf);
}

/// <summary>
/// A credentialed practitioner (HQMS M13): holds licences/certifications (each primary-source
/// verified) and delineated clinical privileges (requested, then granted or denied by the
/// credentials committee). A practitioner may be credentialed only once at least one licence is
/// verified and one privilege granted, is reappointed on a cycle, and can be suspended. The
/// point-of-care check asks whether the practitioner holds a given active privilege right now.
/// </summary>
public sealed class Practitioner : AggregateRoot, ITenantScoped
{
    private readonly List<LicenceCredential> _licences = [];
    private readonly List<Privilege> _privileges = [];

    private Practitioner()
    {
        PractitionerRef = null!;
        FullName = null!;
        Specialty = null!;
    }

    public Guid TenantId { get; set; }
    public string PractitionerRef { get; private set; }
    public string FullName { get; private set; }
    public string Specialty { get; private set; }
    public PractitionerStatus Status { get; private set; }
    public DateOnly? AppointedUntil { get; private set; }
    public string? SuspensionReason { get; private set; }

    public IReadOnlyList<LicenceCredential> Licences => _licences.AsReadOnly();
    public IReadOnlyList<Privilege> Privileges => _privileges.AsReadOnly();

    public static Practitioner Register(string practitionerRef, string fullName, string specialty)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("CRD-001", "A practitioner name is required.");
        }

        if (string.IsNullOrWhiteSpace(specialty))
        {
            throw new DomainException("CRD-002", "A specialty is required.");
        }

        return new Practitioner
        {
            PractitionerRef = practitionerRef,
            FullName = fullName.Trim(),
            Specialty = specialty.Trim(),
            Status = PractitionerStatus.Pending,
        };
    }

    public Guid AddLicence(CredentialType type, string identifier, string issuer, DateOnly expiresOn)
    {
        if (Status == PractitionerStatus.Suspended)
        {
            throw new InvalidStateTransitionException("CRD-010", "Cannot add a licence to a suspended practitioner.");
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new DomainException("CRD-011", "A licence identifier is required.");
        }

        var licence = new LicenceCredential(type, identifier.Trim(), string.IsNullOrWhiteSpace(issuer) ? "Unknown" : issuer.Trim(), expiresOn);
        _licences.Add(licence);
        return licence.Id;
    }

    /// <summary>Records primary-source verification of a licence by an independent verifier.</summary>
    public void VerifyLicence(Guid licenceId, Guid verifierId, string source, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new DomainException("CRD-012", "The verification source is required.");
        }

        var licence = _licences.FirstOrDefault(l => l.Id == licenceId)
            ?? throw new DomainException("CRD-013", "Licence not found.");
        licence.Verify(verifierId, source.Trim(), at);
    }

    public Guid RequestPrivilege(string name)
    {
        if (Status == PractitionerStatus.Suspended)
        {
            throw new InvalidStateTransitionException("CRD-020", "Cannot request a privilege for a suspended practitioner.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("CRD-021", "A privilege name is required.");
        }

        if (_privileges.Any(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)
                                 && p.Status is PrivilegeStatus.Requested or PrivilegeStatus.Granted))
        {
            throw new DomainException("CRD-022", "That privilege is already requested or granted.");
        }

        var privilege = new Privilege(name.Trim());
        _privileges.Add(privilege);
        return privilege.Id;
    }

    public void GrantPrivilege(Guid privilegeId, DateOnly? grantedUntil)
    {
        var privilege = LoadRequested(privilegeId);
        privilege.Grant(grantedUntil);
    }

    public void DenyPrivilege(Guid privilegeId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("CRD-023", "A denial reason is required.");
        }

        LoadRequested(privilegeId).Deny(reason.Trim());
    }

    private Privilege LoadRequested(Guid privilegeId)
    {
        var privilege = _privileges.FirstOrDefault(p => p.Id == privilegeId)
            ?? throw new DomainException("CRD-024", "Privilege not found.");
        if (privilege.Status != PrivilegeStatus.Requested)
        {
            throw new InvalidStateTransitionException("CRD-025", $"A privilege in state {privilege.Status} cannot be decided.");
        }

        return privilege;
    }

    /// <summary>
    /// Completes initial credentialing (Pending ⇒ Credentialed). Requires at least one verified
    /// licence and one granted privilege — the committee cannot appoint an unverified practitioner.
    /// </summary>
    public void Credential(DateOnly appointedUntil)
    {
        if (Status != PractitionerStatus.Pending)
        {
            throw new InvalidStateTransitionException("CRD-030", "Only a pending practitioner can be credentialed.");
        }

        RequireEvidence();
        AppointedUntil = appointedUntil;
        Status = PractitionerStatus.Credentialed;
    }

    /// <summary>Reappointment cycle (Credentialed ⇒ Credentialed with a new appointment end).</summary>
    public void Reappoint(DateOnly appointedUntil)
    {
        if (Status != PractitionerStatus.Credentialed)
        {
            throw new InvalidStateTransitionException("CRD-031", "Only a credentialed practitioner can be reappointed.");
        }

        RequireEvidence();
        AppointedUntil = appointedUntil;
    }

    private void RequireEvidence()
    {
        if (!_licences.Any(l => l.VerificationStatus == VerificationStatus.Verified))
        {
            throw new DomainException("CRD-032", "At least one primary-source-verified licence is required.");
        }

        if (!_privileges.Any(p => p.Status == PrivilegeStatus.Granted))
        {
            throw new DomainException("CRD-033", "At least one granted privilege is required.");
        }
    }

    public void Suspend(string reason)
    {
        if (Status != PractitionerStatus.Credentialed)
        {
            throw new InvalidStateTransitionException("CRD-040", "Only a credentialed practitioner can be suspended.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("CRD-041", "A suspension reason is required.");
        }

        SuspensionReason = reason.Trim();
        Status = PractitionerStatus.Suspended;
    }

    public void Reinstate()
    {
        if (Status != PractitionerStatus.Suspended)
        {
            throw new InvalidStateTransitionException("CRD-042", "Only a suspended practitioner can be reinstated.");
        }

        SuspensionReason = null;
        Status = PractitionerStatus.Credentialed;
    }

    /// <summary>
    /// The point-of-care check: does this practitioner hold the named privilege as an active grant
    /// right now? Only a credentialed (not suspended) practitioner can exercise a privilege.
    /// </summary>
    public bool HasActivePrivilege(string name, DateOnly asOf) =>
        Status == PractitionerStatus.Credentialed
        && _privileges.Any(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) && p.IsActive(asOf));
}
