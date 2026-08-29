using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using NT.QAMS.Application.Abstractions;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// ADR-0010: cross-module reads are allowed on the QUERY side; a COMMAND handler
/// mutates only its own module's aggregate and reaches another module only to
/// READ-and-decide (a guard), never to write. This test pins the current set of
/// command handlers that touch a foreign module's domain namespace: each entry
/// is a verified read-guard, and the build fails on any NEW cross-module command
/// dependency so the author must confirm it is a read, not a write, and add it
/// here on the record. SHRINK-ONLY: remove an entry when a handler stops
/// reaching across; never relax the rule.
/// </summary>
public class CommandHandlerScopeTests
{
    private static readonly Assembly Application = typeof(NT.QAMS.Application.DependencyInjection).Assembly;

    /// <summary>
    /// Cross-cutting domain modules every handler legitimately references: the
    /// permission + actor vocabulary (Authorization, IdentityAccess), the Part 11
    /// signing/audit ledger (ComplianceLedger — the e-signature ceremony), and
    /// file attachments (Files). These are shared infrastructure, not another
    /// module's business aggregate.
    /// </summary>
    private static readonly HashSet<string> SharedCrossCutting = new(StringComparer.Ordinal)
    {
        "Authorization", "IdentityAccess", "ComplianceLedger", "Files",
    };

    /// <summary>
    /// Approved cross-module reads by command handlers (ADR-0010). Key: handler
    /// type name. Value: the foreign domain modules it reads to decide, with why.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> ApprovedReadGuards =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // ── Read-guards: the handler reads the foreign module only to DECIDE,
            //    then writes its OWN aggregate. ──
            //
            // EVD-004: an evidence link verifies the cited record exists in-tenant
            // across every register a standard can reference, before the link is
            // written to the Accreditation aggregate.
            ["LinkEvidenceHandler"] =
            [
                "AuditManagement", "Committees", "DocumentControl", "Improvement",
                "IncidentReporting", "QualityIndicators", "TrainingManagement",
            ],
            // The interested-party context issue checks the risk exists before
            // LinkRisk writes the ContextIssue (Organization) aggregate.
            ["ContextIssueHandlers"] = ["RiskGovernance"],
            // Sign-in / password change read the tenant's settings (lockout, MFA
            // policy) to decide; they write only the user/session.
            ["LoginHandler"] = ["Tenancy"],
            ["ChangePasswordHandler"] = ["Tenancy"],
            // Scope + test-authorization validate the branch/department exist
            // before writing the user/authorization aggregate.
            ["SetUserScopeHandler"] = ["Organization"],
            ["GrantTestAuthorizationHandler"] = ["Organization"],

            // ── The one sanctioned cross-module WRITE (ADR-0010, as-built). ──
            // The incident→CAPA convergence (HQMS M03) creates a Nonconformance
            // (Improvement) from an incident and links it back, in one
            // transaction — the deliberate exception, singular and documented.
            ["RaiseCapaFromIncidentHandler"] = ["Improvement"],
        };

    [Fact]
    public void Command_handlers_touch_no_foreign_module_beyond_the_approved_read_guards()
    {
        var location = Application.Location;
        using var module = ModuleDefinition.ReadModule(location);

        var handlerTypes = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && ImplementsCommandHandler(t))
            .ToList();

        var violations = new List<string>();

        foreach (var handler in handlerTypes)
        {
            var ownModule = ModuleOf(handler.Namespace);
            if (ownModule is null)
            {
                continue;
            }

            var cecilType = module.GetType(handler.FullName);
            if (cecilType is null)
            {
                continue;
            }

            var foreignModules = ForeignDomainModules(cecilType, ownModule);
            var approved = ApprovedReadGuards.GetValueOrDefault(handler.Name, []);
            var unapproved = foreignModules.Where(m => !approved.Contains(m)).OrderBy(m => m).ToList();

            if (unapproved.Count > 0)
            {
                violations.Add($"{handler.Name} ({ownModule}) → {string.Join(", ", unapproved)}");
            }
        }

        violations.Should().BeEmpty(
            "a command handler writes only its own module's aggregate and may read another "
            + "module only to decide (ADR-0010) — a new cross-module command dependency must be "
            + "confirmed a read-guard and listed in ApprovedReadGuards, or removed:\n"
            + string.Join("\n", violations));
    }

    private static bool ImplementsCommandHandler(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType
            && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
                || i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)));

    /// <summary>The domain module a namespace belongs to (the segment after NT.QAMS.Application), or null.</summary>
    private static string? ModuleOf(string? ns)
    {
        const string prefix = "NT.QAMS.Application.";
        if (ns is null || !ns.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns[prefix.Length..];
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest[..dot];
    }

    /// <summary>
    /// Foreign domain modules this handler's IL references — every
    /// NT.QAMS.Domain.&lt;Module&gt; touched by a field, method signature or
    /// method-body instruction, excluding the handler's own module.
    /// </summary>
    private static HashSet<string> ForeignDomainModules(TypeDefinition type, string ownModule)
    {
        const string domainPrefix = "NT.QAMS.Domain.";
        var found = new HashSet<string>(StringComparer.Ordinal);

        // Async handler logic compiles into nested state-machine types
        // (<Handle>d__N.MoveNext), so the aggregate access lives there, not in
        // the handler's own methods — scan the handler and all its nested types.
        var toScan = new List<TypeDefinition> { type };
        for (var i = 0; i < toScan.Count; i++)
        {
            toScan.AddRange(toScan[i].NestedTypes);
        }

        void ConsiderName(string? fullName)
        {
            if (fullName is null || !fullName.StartsWith(domainPrefix, StringComparison.Ordinal))
            {
                return;
            }

            var rest = fullName[domainPrefix.Length..];
            var dot = rest.IndexOf('.');
            var mod = dot < 0 ? rest : rest[..dot];
            if (mod != ownModule && !SharedCrossCutting.Contains(mod))
            {
                found.Add(mod);
            }
        }

        // Unwrap generic instances (DbSet<ForeignAggregate>, Task<...>) so a
        // foreign aggregate reached only as a type argument is still counted.
        void Consider(TypeReference? tr)
        {
            if (tr is null)
            {
                return;
            }

            ConsiderName(tr.FullName);
            if (tr is GenericInstanceType git)
            {
                foreach (var arg in git.GenericArguments)
                {
                    Consider(arg);
                }
            }
        }

        foreach (var method in toScan.SelectMany(t => t.Methods))
        {
            Consider(method.ReturnType);
            foreach (var p in method.Parameters)
            {
                Consider(p.ParameterType);
            }

            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instr in method.Body.Instructions)
            {
                switch (instr.Operand)
                {
                    case GenericInstanceMethod gim:
                        Consider(gim.DeclaringType);
                        Consider(gim.ReturnType);
                        foreach (var arg in gim.GenericArguments)
                        {
                            Consider(arg);
                        }

                        foreach (var p in gim.Parameters)
                        {
                            Consider(p.ParameterType);
                        }

                        break;
                    case MethodReference mr:
                        Consider(mr.DeclaringType);
                        Consider(mr.ReturnType);
                        foreach (var p in mr.Parameters)
                        {
                            Consider(p.ParameterType);
                        }

                        break;
                    case FieldReference fr:
                        Consider(fr.DeclaringType);
                        Consider(fr.FieldType);
                        break;
                    case TypeReference tr:
                        Consider(tr);
                        break;
                }
            }
        }

        return found;
    }
}
