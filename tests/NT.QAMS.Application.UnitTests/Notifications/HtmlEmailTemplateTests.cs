using FluentAssertions;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Infrastructure.Email;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Notifications;

/// <summary>
/// The HTML e-mail template must produce a self-contained, branded document and —
/// critically — HTML-escape every caller-supplied value so a notification template
/// or a record title can never inject markup into the message.
/// </summary>
public sealed class HtmlEmailTemplateTests
{
    private static EmailMessage Message(string subject, string body, string? brand = null) =>
        new("to@lab.test", subject, body, "Acme Quality", "quality@acme.test", null, "Acme Laboratory", brand, null);

    [Fact]
    public void Render_produces_a_branded_self_contained_html_document()
    {
        var html = HtmlEmailTemplate.Render(Message("NC-2026-0001 awaits triage", "A nonconformance needs your attention."));

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("Acme Laboratory");
        html.Should().Contain("NC-2026-0001 awaits triage");
        html.Should().Contain("A nonconformance needs your attention.");
        html.Should().NotContain("http://", "the template inlines everything — no external assets");
    }

    [Fact]
    public void A_configured_brand_colour_is_used_as_the_accent()
    {
        var html = HtmlEmailTemplate.Render(Message("s", "b", "#00B2A9"));
        html.Should().Contain("#00B2A9");
    }

    [Fact]
    public void Caller_supplied_text_is_html_escaped()
    {
        var html = HtmlEmailTemplate.Render(Message("<script>alert(1)</script>", "body & <b>bold</b>"));

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
        html.Should().Contain("&amp;");
    }
}
