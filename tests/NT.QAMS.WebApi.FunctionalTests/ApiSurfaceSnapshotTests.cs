using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-6 finding ARCH-005: the public API surface is a CONTRACT. This test
/// snapshots every route+method from the OpenAPI document against the
/// checked-in baseline (ApiSurface.approved.txt); an unreviewed addition,
/// rename, or removal fails CI. To accept an intentional change, review the
/// .received.txt this test writes and copy it over the approved file in the
/// same commit as the API change (per the ADR-0004 evolution policy).
/// </summary>
public sealed class ApiSurfaceSnapshotTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private static string SourceDirectory([CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;

    [Fact]
    public async Task The_route_surface_matches_the_approved_snapshot()
    {
        // The OpenAPI document is mapped in Development only.
        using var dev = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = dev.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var surface = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        var approvedPath = Path.Combine(SourceDirectory(), "ApiSurface.approved.txt");
        var receivedPath = Path.Combine(SourceDirectory(), "ApiSurface.received.txt");
        await File.WriteAllLinesAsync(receivedPath, surface);

        File.Exists(approvedPath).Should().BeTrue(
            $"the baseline must be checked in — review {receivedPath} and commit it as the approved file");

        var approved = (await File.ReadAllLinesAsync(approvedPath))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        surface.Should().Equal(approved,
            "an API-surface change must be intentional: review ApiSurface.received.txt and " +
            "update the approved snapshot in the same commit (ADR-0004)");

        File.Delete(receivedPath); // identical — leave no noise behind
    }
}
