using FluentAssertions;
using NT.QAMS.Domain.Credentialing;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Credentialing;

public class PractitionerTests
{
    private static readonly Guid Verifier = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 1);

    private static Practitioner Registered() => Practitioner.Register("PRC-1", "Dr Alice Roe", "Cardiology");

    private static Practitioner Credentialed()
    {
        var p = Registered();
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-100", "Council", Today.AddYears(1));
        p.VerifyLicence(lic, Verifier, "Council register", Now);
        var priv = p.RequestPrivilege("Coronary angiography");
        p.GrantPrivilege(priv, Today.AddYears(2));
        p.Credential(Today.AddYears(2));
        return p;
    }

    [Fact]
    public void Cannot_credential_without_a_verified_licence_and_granted_privilege()
    {
        var p = Registered();
        var noEvidence = () => p.Credential(Today.AddYears(2));
        noEvidence.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-032");

        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-1", "Council", Today.AddYears(1));
        p.VerifyLicence(lic, Verifier, "Council register", Now);
        var stillNoPriv = () => p.Credential(Today.AddYears(2));
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
        var lic = p.AddLicence(CredentialType.Bls, "BLS-9", "AHA", Today.AddYears(1));
        var act = () => p.VerifyLicence(lic, Verifier, " ", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-012");
    }

    [Fact]
    public void Cannot_request_the_same_privilege_twice_while_open()
    {
        var p = Registered();
        p.RequestPrivilege("Endoscopy");
        var again = () => p.RequestPrivilege("endoscopy");
        again.Should().Throw<DomainException>().Which.Code.Should().Be("CRD-022");
    }

    [Fact]
    public void Only_a_requested_privilege_can_be_decided()
    {
        var p = Registered();
        var priv = p.RequestPrivilege("Endoscopy");
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
        p.Reappoint(Today.AddYears(4));
        p.AppointedUntil.Should().Be(Today.AddYears(4));
    }
}
