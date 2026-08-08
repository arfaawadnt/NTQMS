using FluentAssertions;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Notifications;

public class TenantMailSettingsTests
{
    [Fact]
    public void Create_sets_the_sender_identity_and_announces_the_change()
    {
        var m = TenantMailSettings.Create("Acme Quality", "quality@acme.test", "reply@acme.test", true, "#1E3A5F", "Confidential.");

        m.FromName.Should().Be("Acme Quality");
        m.FromAddress.Should().Be("quality@acme.test");
        m.ReplyTo.Should().Be("reply@acme.test");
        m.Enabled.Should().BeTrue();
        m.BrandColor.Should().Be("#1E3A5F");
        m.DomainEvents.OfType<MailSettingsChanged>().Should().ContainSingle();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("two@@at.test")]
    public void A_bad_sender_address_is_rejected(string address)
    {
        var act = () => TenantMailSettings.Create("N", address, null, true, null, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MAIL-002");
    }

    [Fact]
    public void A_bad_reply_to_is_rejected()
    {
        var act = () => TenantMailSettings.Create("N", "ok@lab.test", "bad", true, null, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MAIL-003");
    }

    [Theory]
    [InlineData("1E3A5F")]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    public void A_bad_brand_colour_is_rejected(string color)
    {
        var act = () => TenantMailSettings.Create("N", "ok@lab.test", null, true, color, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MAIL-004");
    }

    [Fact]
    public void Update_replaces_the_identity_and_can_disable_mail()
    {
        var m = TenantMailSettings.Create("Old", "old@lab.test", null, true, null, null);

        m.Update("New", "new@lab.test", null, false, null, null);

        m.FromName.Should().Be("New");
        m.FromAddress.Should().Be("new@lab.test");
        m.Enabled.Should().BeFalse();
    }
}
