using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NT.QAMS.WebApi.Middleware;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-1 finding DB-009/VAL-003, HTTP half: a lost optimistic-concurrency
/// race (DbUpdateConcurrencyException from the xmin token) maps to a stable
/// RFC 7807 conflict — 409 with code CONCURRENCY-409 — so clients can reload
/// and retry instead of misreading it as a server fault.
/// </summary>
public sealed class ConcurrencyConflictMappingTests
{
    [Fact]
    public async Task Concurrency_exception_maps_to_409_with_the_stable_code()
    {
        var handler = new DomainExceptionHandler();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            httpContext, new DbUpdateConcurrencyException("row version mismatch"), CancellationToken.None);

        handled.Should().BeTrue("the handler owns concurrency conflicts");
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        httpContext.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(httpContext.Response.Body);
        problem.RootElement.GetProperty("code").GetString()
            .Should().Be(DomainExceptionHandler.ConcurrencyConflictCode);
    }
}
