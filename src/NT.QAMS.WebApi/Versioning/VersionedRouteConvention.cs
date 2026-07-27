using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace NT.QAMS.WebApi.Versioning;

/// <summary>
/// API-001: every literal <c>api/...</c> attribute route ALSO resolves at
/// <c>api/v{version:apiVersion}/...</c> — one central convention instead of
/// hand-editing 41 controllers. Controllers carry no version attributes, so
/// they are implicitly v1.0; unversioned legacy paths keep working through
/// <c>AssumeDefaultVersionWhenUnspecified</c>. Contract-evolution rules live
/// in docs/adr/ADR-0004-api-versioning.md.
/// </summary>
public sealed class VersionedRouteConvention : IApplicationModelConvention
{
    private const string ApiPrefix = "api/";
    private const string VersionedPrefix = "api/v{version:apiVersion}/";

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var literalSelectors = controller.Selectors
                .Where(selector =>
                    selector.AttributeRouteModel?.Template?.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            foreach (var selector in literalSelectors)
            {
                var template = selector.AttributeRouteModel!.Template!;
                controller.Selectors.Add(new SelectorModel(selector)
                {
                    AttributeRouteModel = new AttributeRouteModel
                    {
                        Template = VersionedPrefix + template[ApiPrefix.Length..],
                    },
                });
            }
        }
    }
}
