using System.Reflection;
using FluentAssertions;
using NT.QAMS.Application.Abstractions;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// Phase-4 finding CQRS-003 as a merge gate: every command in the Application
/// assembly must declare exactly one authorization policy. The runtime
/// behavior denies unannotated commands anyway (fail closed) — this test
/// turns the omission into a CI failure instead of a production 422.
/// </summary>
public class CommandPolicyTests
{
    private static readonly Assembly Application =
        typeof(NT.QAMS.Application.DependencyInjection).Assembly;

    private static bool IsCommand(Type type) =>
        type is { IsAbstract: false, IsInterface: false } &&
        type.GetInterfaces().Any(i =>
            i == typeof(ICommand) ||
            (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

    [Fact]
    public void Every_command_declares_exactly_one_authorization_policy()
    {
        var commands = Application.GetTypes().Where(IsCommand).ToList();
        commands.Should().NotBeEmpty("the scan itself must be alive");

        var unannotated = commands
            .Where(c => c.GetCustomAttributes<CommandPolicyAttribute>(inherit: false).Count() != 1)
            .Select(c => c.FullName)
            .ToList();

        unannotated.Should().BeEmpty(
            "every command carries exactly one CommandPolicy attribute — the behavior denies it otherwise");
    }
}
