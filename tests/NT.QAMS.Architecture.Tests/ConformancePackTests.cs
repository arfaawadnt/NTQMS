using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentValidation;
using Mono.Cecil;
using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;
using NT.QAMS.WebApi.Authorization;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// The Architecture Conformance Verification Pack (companion to the enterprise
/// architecture standard), translated into executable rules. Each test cites the
/// pack rule it enforces. Once green, a later module cannot silently violate the
/// rule — the build fails instead.
/// </summary>
public static class ConformanceInputs
{
    public static readonly Assembly Domain = typeof(NT.QAMS.Domain.Tenancy.Tenant).Assembly;
    public static readonly Assembly Application = typeof(NT.QAMS.Application.DependencyInjection).Assembly;
    public static readonly Assembly Contracts = typeof(NT.QAMS.Contracts.Tenancy.TenantDto).Assembly;
    public static readonly Assembly Infrastructure = typeof(NT.QAMS.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly WebApi = typeof(RequirePermissionAttribute).Assembly;

    /// <summary>Concrete persisted domain types — aggregates and owned children.</summary>
    public static IReadOnlyList<Type> DomainEntities { get; } =
        Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Entity).IsAssignableFrom(t))
            .OrderBy(t => t.FullName)
            .ToList();

    /// <summary>Walks a type's base chain (excluding object).</summary>
    public static IEnumerable<Type> BaseChain(Type type)
    {
        for (var b = type.BaseType; b is not null && b != typeof(object); b = b.BaseType)
        {
            yield return b;
        }
    }

    /// <summary>
    /// Reads a shrink-only approved snapshot next to this test's source, in the
    /// same idiom as <c>ApiSurface.approved.txt</c>: the file records the as-built
    /// state at the v1.54 baseline plus the audited HQMS delta. A NEW entry fails
    /// the gate; entries may only ever be removed (and the file trimmed with them).
    /// </summary>
    public static IReadOnlyCollection<string> ApprovedSnapshot(string fileName, [CallerFilePath] string thisFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(thisFile)!, fileName);
        File.Exists(path).Should().BeTrue($"the approved snapshot {fileName} must live next to the test source");
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Asserts a scanned set equals its approved snapshot, naming drift in both directions.</summary>
    public static void ShouldMatchSnapshot(IEnumerable<string> current, string fileName, string ruleExplanation)
    {
        var approved = ApprovedSnapshot(fileName);
        var actual = current.ToHashSet(StringComparer.Ordinal);

        var added = actual.Except(approved).OrderBy(x => x).ToList();
        var removed = approved.Except(actual).OrderBy(x => x).ToList();

        added.Should().BeEmpty(
            $"{ruleExplanation} New offenders (fix them or consciously extend {fileName}):\n{{0}}",
            string.Join("\n", added));
        removed.Should().BeEmpty(
            $"these entries no longer occur — trim them from {fileName} so the snapshot only shrinks:\n{{0}}",
            string.Join("\n", removed));
    }
}

/// <summary>
/// Pack rules 6–9: the domain model protects itself structurally. The stamped
/// properties (<c>TenantId</c>, the <see cref="IAuditable"/> audit columns) are
/// the standard's own sanctioned exceptions — they are written by persistence
/// interceptors, never by callers.
/// </summary>
public class DomainModelIntegrityTests
{
    private static readonly string[] StampedByInterceptor =
    [
        nameof(ITenantScoped.TenantId),
        nameof(AggregateRoot.CreatedAtUtc), nameof(AggregateRoot.CreatedBy),
        nameof(AggregateRoot.CreatedByUserId), nameof(AggregateRoot.ModifiedAtUtc),
        nameof(AggregateRoot.ModifiedBy),
    ];

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    /// <summary>Pack rule 6: entities and aggregates expose no public mutable setters.</summary>
    [Fact]
    public void Domain_entities_have_no_public_setters_outside_the_stamped_columns()
    {
        var offenders = ConformanceInputs.DomainEntities
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } setter
                            && !IsInitOnly(setter)
                            && !StampedByInterceptor.Contains(p.Name)
                            // IAllocatable mandates settable BranchId/DepartmentId: the
                            // organizational allocation is data (set at creation, movable),
                            // guarded on write by OrgScopeGuardInterceptor. NOTE: eight HQMS
                            // aggregates declare these columns WITHOUT the IAllocatable marker,
                            // so neither the org-scope read filter nor the write guard applies
                            // to them — recorded as conformance finding, decision pending
                            // (deliberate tenant-wide safety visibility vs. omission).
                            && p.Name is not (nameof(IAllocatable.BranchId) or nameof(IAllocatable.DepartmentId)))
                .Select(p => $"{t.FullName}.{p.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "state changes travel through aggregate methods that guard invariants; a public setter bypasses them. "
            + "Offenders:\n{0}", string.Join("\n", offenders));
    }

    /// <summary>Pack rule 7: child collections are read-only views over private fields.</summary>
    [Fact]
    public void Domain_entities_expose_no_mutable_collection_types()
    {
        var mutable = new[]
        {
            typeof(List<>), typeof(HashSet<>), typeof(ICollection<>), typeof(IList<>),
            typeof(ISet<>), typeof(Dictionary<,>), typeof(IDictionary<,>),
        };

        var offenders = ConformanceInputs.DomainEntities
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType.IsGenericType
                            && mutable.Contains(p.PropertyType.GetGenericTypeDefinition()))
                .Select(p => $"{t.FullName}.{p.Name} : {p.PropertyType.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "a mutable collection lets callers add/remove children without the aggregate's guard methods");
    }

    /// <summary>
    /// Pack rule 8: every persisted entity keeps a non-public parameterless
    /// constructor for EF and offers no public parameterless constructor that
    /// would let a caller skip the factory/invariants.
    /// </summary>
    [Fact]
    public void Domain_entities_have_a_non_public_parameterless_constructor_for_EF()
    {
        var offenders = new List<string>();
        foreach (var type in ConformanceInputs.DomainEntities)
        {
            var parameterless = type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null, Type.EmptyTypes, modifiers: null);

            if (parameterless is null)
            {
                offenders.Add($"{type.FullName}: no parameterless constructor (EF materialization)");
            }
            else if (parameterless.IsPublic)
            {
                offenders.Add($"{type.FullName}: parameterless constructor is public (bypasses invariants)");
            }
        }

        offenders.Should().BeEmpty();
    }

    /// <summary>Pack rule 9: domain events are immutable.</summary>
    [Fact]
    public void Domain_events_are_immutable()
    {
        var events = ConformanceInputs.Domain.GetTypes()
            .Where(t => t is { IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(t))
            .ToList();
        events.Should().NotBeEmpty("the scan itself must be alive");

        var offenders = events
            .SelectMany(e => e.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
                .Select(p => $"{e.FullName}.{p.Name}"))
            .ToList();

        offenders.Should().BeEmpty("an event is a recorded fact; offenders:\n{0}", string.Join("\n", offenders));
    }

    /// <summary>
    /// The repo's own rule (ntqms-architecture §2.1): events do not carry
    /// <c>TenantId</c> — the ledger attributes tenancy itself. Fifty
    /// pre-hardening-era events (and M18's <c>ChangeRatified</c>, which copied its
    /// file's local pattern) are grandfathered in the snapshot; no new event may
    /// join them.
    /// </summary>
    [Fact]
    public void No_new_domain_event_carries_a_tenant_id()
    {
        var carriers = ConformanceInputs.Domain.GetTypes()
            .Where(t => t is { IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(t))
            .Where(t => t.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance) is not null)
            .Select(t => t.FullName!);

        ConformanceInputs.ShouldMatchSnapshot(carriers, "EventsCarryingTenantId.approved.txt",
            "events must not carry TenantId — the outbox/ledger attributes tenancy itself.");
    }

    /// <summary>
    /// Pack rule 28 (structural half): a tenant column only gets the EF global
    /// filter and FORCE RLS treatment when the type declares the tenancy marker.
    /// An entity with a TenantId property that skips the interface silently opts
    /// out of both — this makes that impossible.
    /// </summary>
    [Fact]
    public void Every_entity_with_a_tenant_column_declares_the_tenancy_marker()
    {
        var offenders = new List<string>();
        foreach (var type in ConformanceInputs.DomainEntities)
        {
            var tenantId = type.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance);
            if (tenantId is null)
            {
                continue;
            }

            if (tenantId.PropertyType == typeof(Guid) && !typeof(ITenantScoped).IsAssignableFrom(type))
            {
                offenders.Add($"{type.FullName}: Guid TenantId without ITenantScoped");
            }

            if (tenantId.PropertyType == typeof(Guid?) && !typeof(IOptionallyTenantScoped).IsAssignableFrom(type))
            {
                offenders.Add($"{type.FullName}: Guid? TenantId without IOptionallyTenantScoped");
            }
        }

        offenders.Should().BeEmpty(
            "the EF tenant filter and the audit attribution key off the marker interfaces, not the column");
    }
}

/// <summary>Pack rules 11–15: the CQRS pipeline stays in shape.</summary>
public class CqrsPipelineRulesTests
{
    private static bool ImplementsOpenInterface(Type type, string interfaceName) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == interfaceName);

    /// <summary>Pack rule 11: request handlers live in Application only.</summary>
    [Fact]
    public void No_request_handler_exists_outside_the_Application_assembly()
    {
        var offenders = new[] { ConformanceInputs.WebApi, ConformanceInputs.Infrastructure }
            .SelectMany(a => a.GetTypes())
            .Where(t => ImplementsOpenInterface(t, "IRequestHandler`1") || ImplementsOpenInterface(t, "IRequestHandler`2"))
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty("use cases are Application slices; hosting layers only dispatch");
    }

    /// <summary>
    /// Pack rule 12, scoped to where it protects something: every command that
    /// carries free text must have a validator, because text-column bounds live in
    /// <c>MaximumLength</c> rules, not the schema (columns ≥1000 are <c>text</c>).
    /// Id-/enum-/number-only lifecycle commands (sign-offs, closes, recalculations)
    /// have no shape to bound; the as-built codebase leaves them validator-less.
    /// Pre-existing text-carrying gaps are grandfathered in the snapshot and may
    /// only shrink.
    /// </summary>
    [Fact]
    public void Every_text_carrying_command_has_a_FluentValidation_validator()
    {
        var commands = ConformanceInputs.Application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.GetInterfaces().Any(i =>
                            i.FullName == "NT.QAMS.Application.Abstractions.ICommand"
                            || (i.IsGenericType && i.GetGenericTypeDefinition().Name == "ICommand`1")))
            .ToList();
        commands.Should().NotBeEmpty("the scan itself must be alive");

        var validated = ConformanceInputs.Application.GetTypes()
            .Select(t => ConformanceInputs.BaseChain(t)
                .FirstOrDefault(b => b.IsGenericType && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>)))
            .Where(b => b is not null)
            .Select(b => b!.GetGenericArguments()[0])
            .ToHashSet();

        static bool CarriesText(Type command) =>
            command.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.PropertyType == typeof(string)
                          || p.PropertyType == typeof(IReadOnlyList<string>)
                          || p.PropertyType == typeof(List<string>));

        var missing = commands
            .Where(c => CarriesText(c) && !validated.Contains(c))
            .Select(c => c.FullName!);

        ConformanceInputs.ShouldMatchSnapshot(missing, "CommandsWithoutValidators.approved.txt",
            "a text-carrying command without a validator reaches the domain with unbounded strings.");
    }

    /// <summary>
    /// Pack rule 13: query handlers read — they never call SaveChanges. Inspected
    /// at IL level (including compiler-generated async state machines), because a
    /// source scan cannot see through partial helpers.
    /// </summary>
    [Fact]
    public void Query_handlers_never_call_SaveChanges()
    {
        using var module = ModuleDefinition.ReadModule(ConformanceInputs.Application.Location);

        var offenders = new List<string>();
        foreach (var type in module.GetTypes())
        {
            var isQueryHandler = type.Interfaces.Any(i =>
                i.InterfaceType.Name == "IQueryHandler`2"
                || (i.InterfaceType is GenericInstanceType { Name: "IRequestHandler`2" } g
                    && g.GenericArguments[0].Name.EndsWith("Query", StringComparison.Ordinal)));
            if (!isQueryHandler)
            {
                continue;
            }

            foreach (var method in EnumerateWithNested(type).SelectMany(t => t.Methods).Where(m => m.HasBody))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is MethodReference callee
                        && callee.Name.StartsWith("SaveChanges", StringComparison.Ordinal))
                    {
                        offenders.Add($"{type.FullName}.{method.Name} calls {callee.Name}");
                    }
                }
            }
        }

        offenders.Should().BeEmpty("a query that writes is a command wearing the wrong policy");
    }

    private static IEnumerable<TypeDefinition> EnumerateWithNested(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(EnumerateWithNested))
        {
            yield return nested;
        }
    }

    /// <summary>Pack rule 14: no IQueryable crosses an assembly boundary.</summary>
    [Fact]
    public void No_public_member_returns_IQueryable()
    {
        static bool IsQueryable(Type t) =>
            t == typeof(System.Linq.IQueryable)
            || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>));

        var offenders = new[] { ConformanceInputs.Application, ConformanceInputs.Contracts }
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => IsQueryable(m.ReturnType))
                .Select(m => $"{t.FullName}.{m.Name}"))
            .ToList();

        offenders.Should().BeEmpty("deferred queries leaking across a boundary execute in the wrong layer");
    }

    /// <summary>Pack rule 15: no generic repository / unit-of-work abstraction exists.</summary>
    [Fact]
    public void No_generic_repository_or_unit_of_work_abstraction_exists()
    {
        string[] banned = ["IRepository`1", "IGenericRepository`1", "IUnitOfWork"];

        var offenders = new[]
            {
                ConformanceInputs.Domain, ConformanceInputs.Application,
                ConformanceInputs.Infrastructure, ConformanceInputs.WebApi,
            }
            .SelectMany(a => a.GetTypes())
            .Where(t => banned.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty("the persistence port is IAppDbContext (ADR-0008); a repository wrapper duplicates it");
    }
}

/// <summary>Pack rules 17–19: the HTTP surface stays thin, gated, and DTO-only.</summary>
public class ApiSurfaceRulesTests
{
    private static IReadOnlyList<Type> Controllers { get; } =
        ConformanceInputs.WebApi.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && ConformanceInputs.BaseChain(t).Any(b => b.Name is "ControllerBase" or "Controller"))
            .OrderBy(t => t.FullName)
            .ToList();

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions() =>
        Controllers.SelectMany(c => c
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => (c, m)));

    /// <summary>Pack rule 17: no action parameter or return type is a domain entity.</summary>
    [Fact]
    public void No_controller_action_exposes_a_domain_type_in_its_signature()
    {
        var offenders = new List<string>();
        foreach (var (controller, action) in Actions())
        {
            var exposed = action.GetParameters().Select(p => p.ParameterType)
                .Concat(Flatten(action.ReturnType))
                .Where(t => t.Assembly == ConformanceInputs.Domain)
                .ToList();

            offenders.AddRange(exposed.Select(t => $"{controller.Name}.{action.Name} exposes {t.Name}"));
        }

        offenders.Should().BeEmpty("the wire shape is Contracts DTOs; entities on the wire freeze the domain");

        static IEnumerable<Type> Flatten(Type t) =>
            t.IsGenericType ? t.GetGenericArguments().SelectMany(Flatten).Prepend(t) : [t];
    }

    /// <summary>
    /// Pack rule 18: every action is authorization-gated at the controller, or is a
    /// recorded entry in <c>UngatedActions.approved.txt</c>. The snapshot is the
    /// as-built decision record: the pre-authentication surface, the incident
    /// safety-culture intake (HQMS M02 — open by requirement, command-policy
    /// checked), and the pre-v1.51 controllers whose reads rely on <c>[Authorize]</c>
    /// only and whose writes are gated by command policies
    /// (<see cref="CommandPolicyTests"/> keeps that layer total). Queries are NOT
    /// gated in the MediatR pipeline (AuthorizationBehavior gates commands only),
    /// so for reads the controller attribute is the only permission check — which
    /// is why a NEW endpoint may not join this file without a decision.
    /// </summary>
    [Fact]
    public void Every_controller_action_is_permission_gated_or_a_recorded_exemption()
    {
        var ungated = new List<string>();
        foreach (var (controller, action) in Actions())
        {
            if (action.GetCustomAttributes<RequirePermissionAttribute>(inherit: true).Any())
            {
                continue;
            }

            // The platform surface is governed by role tiers, not the tenant permission catalogue.
            var attributes = action.GetCustomAttributes(inherit: true)
                .Concat(controller.GetCustomAttributes(inherit: true));
            var roleGated = attributes.Any(a =>
                a.GetType().Name == "AuthorizeAttribute"
                && a.GetType().GetProperty("Roles")?.GetValue(a) is string roles
                && roles.Length > 0);
            if (roleGated)
            {
                continue;
            }

            ungated.Add($"{controller.Name}.{action.Name}");
        }

        ConformanceInputs.ShouldMatchSnapshot(ungated, "UngatedActions.approved.txt",
            "an endpoint with no permission gate and no platform role gate has no read-side "
            + "authorization beyond authentication.");
    }

    /// <summary>
    /// Pack rule 19: controllers hold no persistence dependency. Two pre-baseline
    /// streaming endpoints (PDF/XLSX export packs, file upload/download) were built
    /// directly on <c>IAppDbContext</c> and are grandfathered — recorded as a
    /// conformance finding, not extended.
    /// </summary>
    [Fact]
    public void Controllers_take_no_persistence_dependency()
    {
        string[] grandfathered = ["ExportsController", "FilesController"];

        var offenders = Controllers
            .Where(c => !grandfathered.Contains(c.Name))
            .SelectMany(c => c.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(ctor => ctor.GetParameters())
                .Where(p => p.ParameterType.Name.Contains("DbContext", StringComparison.Ordinal)
                            || p.ParameterType.Assembly == ConformanceInputs.Infrastructure)
                .Select(p => $"{c.Name}({p.ParameterType.Name})"))
            .ToList();

        offenders.Should().BeEmpty(
            "controllers dispatch to ISender; data access belongs to handlers. Offenders:\n{0}",
            string.Join("\n", offenders));
    }
}

/// <summary>
/// Pack rule 27 and the tenancy chapter of the standard: the tenant comes from the
/// JWT claim, never from a request body, command, or query.
/// </summary>
public class TenancyContractRulesTests
{
    [Fact]
    public void No_command_or_query_carries_a_tenant_id()
    {
        var messages = ConformanceInputs.Application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.GetInterfaces().Any(i =>
                            i.Name is "ICommand" or "IQuery"
                            || (i.IsGenericType && i.GetGenericTypeDefinition().Name is "ICommand`1" or "IQuery`1")))
            .ToList();
        messages.Should().NotBeEmpty("the scan itself must be alive");

        var offenders = messages
            .Where(m => m.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance) is not null)
            // VerifyChainQuery's TenantId exists for the elevated nightly sweep that
            // iterates every tenant with no ambient tenant context; both HTTP call
            // sites (ComplianceController.VerifyChain, ExportsController audit pack)
            // fill it from the caller's own JWT tenant claim, never from the request.
            .Where(m => m.FullName != "NT.QAMS.Application.ComplianceLedger.VerifyChainQuery")
            .Select(m => m.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "a client-suppliable TenantId lets a caller aim a use case at another tenant; "
            + "the tenant is resolved from the authenticated claim only. Offenders:\n{0}",
            string.Join("\n", offenders));
    }

    [Fact]
    public void No_request_contract_carries_a_tenant_id()
    {
        var offenders = ConformanceInputs.Contracts.GetTypes()
            .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal)
                        || t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => t.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance) is not null)
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty("request DTOs must not offer a tenant field for model binding to fill");
    }
}

/// <summary>
/// Pack rules 3/4 (framework isolation, completing LayerRulesTests) and 25
/// (structured logging only).
/// </summary>
public class FrameworkIsolationRulesTests
{
    [Fact]
    public void Domain_references_only_the_SharedKernel_and_the_base_class_library()
    {
        var references = ConformanceInputs.Domain.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        var foreign = references
            .Where(name => name != "NT.QAMS.SharedKernel"
                           && !name.StartsWith("System", StringComparison.Ordinal)
                           && name is not ("netstandard" or "mscorlib" or "System"))
            .ToList();

        foreign.Should().BeEmpty("the domain is persistence-, transport- and framework-ignorant by definition");
    }

    [Fact]
    public void Application_references_no_database_driver_or_logging_sink()
    {
        ConformanceInputs.Application.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(name => name == "Npgsql" || name!.StartsWith("Serilog"),
                "Application persists through the IAppDbContext port (ADR-0008) and logs through "
                + "Microsoft.Extensions.Logging abstractions; drivers and sinks are Infrastructure concerns");
    }

    /// <summary>Pack rule 25: no unstructured console logging anywhere in src.</summary>
    [Fact]
    public void No_source_file_calls_Console_WriteLine()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();
        foreach (var project in new[] { "src" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("Console.WriteLine", StringComparison.Ordinal))
                    {
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                    }
                }
            }
        }

        offenders.Should().BeEmpty("logs are structured (Serilog via ILogger); console writes vanish in production");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NT.QAMS.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull();
        return dir!.FullName;
    }
}

/// <summary>
/// Keeps the module-boundary gate exhaustive: a bounded context added under
/// <c>NT.QAMS.Domain</c> must appear in <see cref="ModuleBoundaryTests"/>' list
/// (or be a written exemption), otherwise it ships unguarded — which is exactly
/// how the twelve HQMS contexts initially escaped the gate.
/// </summary>
public class ModuleListExhaustivenessTests
{
    /// <summary>
    /// <c>Authorization</c> hosts the permission catalogue and role aggregate that
    /// every layer names constants from; it is deliberately outside the pairwise
    /// isolation matrix.
    /// </summary>
    private static readonly string[] Exempt = ["Authorization"];

    [Fact]
    public void Every_domain_namespace_is_covered_by_the_module_boundary_gate()
    {
        const string prefix = "NT.QAMS.Domain.";

        var discovered = ConformanceInputs.Domain.GetTypes()
            .Select(t => t.Namespace)
            .Where(ns => ns is not null && ns.StartsWith(prefix, StringComparison.Ordinal))
            .Select(ns => ns![prefix.Length..].Split('.')[0])
            .Distinct()
            .Except(Exempt)
            .OrderBy(m => m)
            .ToList();

        discovered.Should().BeEquivalentTo(ModuleBoundaryTests.Modules,
            "each bounded context must be in the pairwise isolation matrix — an unlisted module is unguarded");
    }
}
