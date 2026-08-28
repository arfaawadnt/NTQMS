using FluentAssertions;
using NT.QAMS.Domain.Credentialing;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Credentialing;

public class PractitionerTests
{
    private static readonly Guid Verifier = Guid.CreateVersion7();
    private static readonly Guid Adder = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 1);

    private static Practitioner Registered() => Practitioner.Register("PRC-1", "Dr Alice Roe", "Cardiology");

    private static Practitioner Credentialed()
    {
        var p = Registered();
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-100", "Council", Today.AddYears(1), Adder);
        p.VerifyLicence(lic, Verifier, "Council register", Now);
        var priv = p.RequestPrivilege("Coronary angiography", Today);
        p.GrantPrivilege(priv, Today.AddYears(2));
        p.Credential(Today.AddYears(2), Today);
        return p;
    }

    [Fact]
    public void Cannot_credential_without_a_verified_licence_and_granted_privilege()
    {
        var p = Registered();
        var noEvidence = () => p.Credential(Today.AddYears(2), Today);
        noEvidence.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-032");

        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-1", "Council", Today.AddYears(1), Adder);
        p.VerifyLicence(lic, Verifier, "Council register", Now);
        var stillNoPriv = () => p.Credential(Today.AddYears(2), Today);
        stillNoPriv.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-033");
    }

    [Fact]
    public void A_verified_licence_and_granted_privilege_allow_credentialing()
    {
        var p = Credentialed();
        p.Status.Should().Be(PractitionerStatus.Credentialed);
        p.AppointedUntil.Should().Be(Today.AddYears(2));
    }

    [Fact]
    public void Verification_requires_a_source()
    {
        var p = Registered();
        var lic = p.AddLicence(CredentialType.Bls, "BLS-9", "AHA", Today.AddYears(1), Adder);
        var act = () => p.VerifyLicence(lic, Verifier, " ", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-012");
    }

    [Fact]
    public void Cannot_request_the_same_privilege_twice_while_open()
    {
        var p = Registered();
        p.RequestPrivilege("Endoscopy", Today);
        var again = () => p.RequestPrivilege("endoscopy", Today);
        again.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-022");
    }

    [Fact]
    public void Only_a_requested_privilege_can_be_decided()
    {
        var p = Registered();
        var priv = p.RequestPrivilege("Endoscopy", Today);
        p.GrantPrivilege(priv, null);
        var regrant = () => p.GrantPrivilege(priv, null);
        regrant.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CRD-025");
    }

    [Fact]
    public void Point_of_care_check_reflects_grant_expiry_and_suspension()
    {
        var p = Credentialed();
        p.HasActivePrivilege("Coronary angiography", Today).Should().BeTrue();
        p.HasActivePrivilege("Coronary angiography", Today.AddYears(3)).Should().BeFalse("the grant has expired");
        p.HasActivePrivilege("Unknown procedure", Today).Should().BeFalse();

        p.Suspend("Under investigation");
        p.HasActivePrivilege("Coronary angiography", Today).Should().BeFalse("a suspended practitioner holds no active privilege");

        p.Reinstate();
        p.HasActivePrivilege("Coronary angiography", Today).Should().BeTrue();
    }

    [Fact]
    public void Reappointment_requires_current_evidence_and_extends_the_appointment()
    {
        var p = Credentialed();
        p.Reappoint(Today.AddYears(4), Today);
        p.AppointedUntil.Should().Be(Today.AddYears(4));
    }

    [Fact]
    public void A_lapsed_appointment_fails_the_point_of_care_check()
    {
        // M-19: the bedside answer must go dark the day the appointment lapses —
        // a stale "holds privilege = true" is clinically dangerous.
        var p = Registered();
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-100", "Council", Today.AddYears(1), Adder);
        p.VerifyLicence(lic, Verifier, "Council register", Now);
        var priv = p.RequestPrivilege("Coronary angiography", Today);
        p.GrantPrivilege(priv, null);
        p.Credential(Today.AddMonths(6), Today);

        p.HasActivePrivilege("Coronary angiography", Today).Should().BeTrue();
        p.HasActivePrivilege("Coronary angiography", Today.AddMonths(7)).Should().BeFalse(
            "the appointment window ended; the privilege grant alone is not enough");
    }

    [Fact]
    public void A_verified_licence_cannot_be_silently_reverified()
    {
        // M-19: PSV is evidence — overwriting it in place would rewrite who
        // verified against which source.
        var p = Registered();
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-100", "Council", Today.AddYears(1), Adder);
        p.VerifyLicence(lic, Verifier, "Council register", Now);

        var again = () => p.VerifyLicence(lic, Guid.CreateVersion7(), "Other source", Now.AddDays(1));
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CRD-014");
    }

    [Fact]
    public void The_verifier_must_be_independent_of_whoever_added_the_credential()
    {
        // M-19: PSV independence (SOD-CRD-001) — claimed by the doc comment,
        // now actually enforced.
        var p = Registered();
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-100", "Council", Today.AddYears(1), Adder);

        var selfVerify = () => p.VerifyLicence(lic, Adder, "Council register", Now);
        selfVerify.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-CRD-001");

        p.VerifyLicence(lic, Verifier, "Council register", Now);
        p.Licences.Single().VerificationStatus.Should().Be(VerificationStatus.Verified);
    }

    [Fact]
    public void A_lapsed_grant_does_not_block_renewal()
    {
        // M-19: PrivilegeStatus.Expired was unreachable and a granted-but-lapsed
        // privilege blocked any re-request forever.
        var p = Registered();
        var priv = p.RequestPrivilege("Endoscopy", Today);
        p.GrantPrivilege(priv, Today.AddYears(-1)); // granted, already lapsed

        var renewal = p.RequestPrivilege("Endoscopy", Today);
        p.Privileges.Should().HaveCount(2, "the lapsed grant stays as history; the renewal opens a new request");
        renewal.Should().NotBeEmpty();
    }

    [Fact]
    public void Stale_evidence_cannot_support_a_reappointment()
    {
        // M-19: RequireEvidence must demand CURRENT evidence — a verified but
        // expired licence is history, not grounds to reappoint.
        var p = Registered();
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-100", "Council", Today.AddMonths(3), Adder);
        p.VerifyLicence(lic, Verifier, "Council register", Now);
        var priv = p.RequestPrivilege("Coronary angiography", Today);
        p.GrantPrivilege(priv, Today.AddYears(2));
        p.Credential(Today.AddYears(2), Today);

        var afterExpiry = Today.AddMonths(4);
        var act = () => p.Reappoint(afterExpiry.AddYears(2), afterExpiry);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-032");
    }
}
