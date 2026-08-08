namespace NT.QAMS.Contracts.Common;

/// <summary>One ordered workflow step of a manual topic (mirrors the in-app help step).</summary>
public sealed record ManualStepDto(string Label, string Detail);

/// <summary>One manual topic: its page, plain-language summary, workflow steps and "how to use" list.</summary>
public sealed record ManualTopicDto(
    string Route,
    string Title,
    string Summary,
    IReadOnlyList<ManualStepDto> Steps,
    IReadOnlyList<string> Usage);

/// <summary>One manual section (a sidebar group) and the topics beneath it.</summary>
public sealed record ManualGroupDto(string Title, IReadOnlyList<ManualTopicDto> Topics);

/// <summary>
/// The assembled User Manual to render as a PDF. The content lives only in the SPA
/// (the trilingual help catalogue), so the caller posts the manual already
/// localized to the active language; the server lays it out and stamps provenance,
/// exactly as the generic page export does — it does not re-author the content.
/// </summary>
public sealed record ManualExportRequest(
    string Language,
    IReadOnlyList<ManualGroupDto> Groups);
