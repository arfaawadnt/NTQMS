using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// Audit finding M-06 / N-06: a declared domain event that is never raised is a
/// dead compliance claim — it looks like a regulated fact reaches the
/// hash-chained ledger, but nothing ever emits it. This test fails the build if
/// any <see cref="DomainEvent"/>-derived type is declared but never instantiated
/// (<c>newobj</c>) anywhere in the Domain assembly. It is the standing guard that
/// keeps the event surface honest.
/// </summary>
public class DomainEventsAreRaisedTests
{
    private static readonly Assembly Domain = typeof(NT.QAMS.Domain.Tenancy.Tenant).Assembly;

    [Fact]
    public void Every_declared_domain_event_is_raised_somewhere()
    {
        using var module = ModuleDefinition.ReadModule(Domain.Location);

        var eventTypes = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(DomainEvent).IsAssignableFrom(t))
            .Select(t => t.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        var instantiated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in AllTypes(module))
        {
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.OpCode == OpCodes.Newobj
                        && instr.Operand is MethodReference { DeclaringType: { } dt })
                    {
                        instantiated.Add(dt.FullName);
                    }
                }
            }
        }

        var dead = eventTypes.Where(e => !instantiated.Contains(e)).OrderBy(e => e).ToList();

        dead.Should().BeEmpty(
            "a declared domain event that is never raised is a dead compliance claim (M-06); "
            + "raise it where its decision fact occurs, or delete the declaration:\n"
            + string.Join("\n", dead));
    }

    private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
    {
        var stack = new Stack<TypeDefinition>(module.Types);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            yield return t;
            foreach (var nested in t.NestedTypes)
            {
                stack.Push(nested);
            }
        }
    }
}
