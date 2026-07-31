using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// Guards the one permanently accepted multi-tenancy deviation.
/// <para>
/// <c>qams.user_account</c> has no row-level security, and cannot: platform
/// administrators have no tenant, so the <c>tenant_isolation</c> predicate —
/// which is false for NULL — would hide them and break authentication, that
/// necessarily runs before a tenant is resolved. The acceptance
/// (<c>SCHEMA-HARDENING-REPORT.md</c> §8) rests on every query being bounded in
/// application code instead: explicitly tenant-filtered, keyed by the
/// authenticated actor's own id, or keyed by an id set already derived from a
/// tenant-filtered query.
/// </para>
/// <para>
/// That is discipline, not structure — so this test makes it structural. A new
/// unbounded query over the user table fails the build rather than leaking
/// across tenants in production, where the database would not stop it.
/// </para>
/// <para>
/// Source-level by necessity: the bound lives in a LINQ predicate, which is not
/// recoverable from compiled IL the way a type reference is (hence NetArchTest,
/// used by the sibling tests here, cannot express this rule).
/// </para>
/// </summary>
public class UserAccountTenantBoundTests
{
    private static string ThisFile([CallerFilePath] string path = "") => path;

    /// <summary>Repository root, walked up from this test's own location.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NT.QAMS.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test must be able to find the repository root");
        return dir!.FullName;
    }

    /// <summary>Entry points to the user table that a query can start from.</summary>
    private static readonly Regex QueryStart = new(
        @"\b(?<recv>db|_db|context|ctx)\.Users\b|\bSet<UserAccount>\(\)",
        RegexOptions.Compiled);

    /// <summary>
    /// A query is bounded when it is scoped to one tenant, to one identified
    /// user, or to a set of ids the caller already obtained through a
    /// tenant-filtered query. Writes and aggregate loads by key are bounded by
    /// the same reasoning.
    /// </summary>
    private static readonly Regex[] Bounds =
    [
        new(@"TenantId\s*==", RegexOptions.Compiled),                     // explicit tenant predicate
        new(@"\.Id\s*==", RegexOptions.Compiled),                         // keyed by a specific user
        new(@"==\s*\w*[Uu]serId\b", RegexOptions.Compiled),               // keyed by the actor
        new(@"\.RoleId\s*==", RegexOptions.Compiled),                     // keyed by a tenant-resolved role
        new(@"Contains\(\s*\w+\.(Id|RoleId)", RegexOptions.Compiled),     // id set from a filtered query
        new(@"\.Add\(|\.AddRange\(|\.Remove\(", RegexOptions.Compiled),   // writes, not reads
        new(@"\.Local\b", RegexOptions.Compiled),                         // change-tracker inspection
    ];

    /// <summary>
    /// A property that merely exposes the set (<c>DbSet&lt;UserAccount&gt; Users =&gt; …</c>)
    /// is a declaration, not a query.
    /// </summary>
    private static readonly Regex Declaration = new(
        @"DbSet<UserAccount>\s+\w+\s*(=>|\{)", RegexOptions.Compiled);

    /// <summary>
    /// The deliberate exception: infrastructure that runs cross-tenant under
    /// <c>Elevate()</c> — provisioning, startup backfills, the outbox — genuinely
    /// has no tenant to bind to. Such a query must say so in a comment directly
    /// above it, so the exemption is a written decision a reviewer can weigh
    /// rather than a silent omission.
    /// </summary>
    private const string CrossTenantMarker = "tenant-unbounded:";

    public static TheoryData<string> SourceProjects() =>
        ["src/NT.QAMS.Application", "src/NT.QAMS.Infrastructure", "src/NT.QAMS.WebApi"];

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Every_user_account_query_is_bounded_by_tenant_or_identity(string project)
    {
        var root = Path.Combine(RepositoryRoot(), project);
        Directory.Exists(root).Should().BeTrue($"{project} must exist");

        var unbounded = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!QueryStart.IsMatch(lines[i]) || Declaration.IsMatch(lines[i]))
                {
                    continue;
                }

                // An explicit, justified cross-tenant exemption on the preceding lines.
                var preamble = string.Join(' ', lines.Skip(Math.Max(0, i - 4)).Take(Math.Min(4, i)));
                if (preamble.Contains(CrossTenantMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                // A LINQ chain spans lines until the statement terminates, so
                // read forward to the semicolon that closes it (bounded, so a
                // malformed file cannot hang the test).
                var statement = string.Join(' ', lines.Skip(i).Take(12));
                var end = statement.IndexOf(';');
                if (end >= 0)
                {
                    statement = statement[..(end + 1)];
                }

                if (!Bounds.Any(b => b.IsMatch(statement)))
                {
                    unbounded.Add($"{Path.GetRelativePath(RepositoryRoot(), file)}:{i + 1} → {lines[i].Trim()}");
                }
            }
        }

        unbounded.Should().BeEmpty(
            "qams.user_account has no RLS (permanently accepted deviation, SCHEMA-HARDENING-REPORT.md §8), "
            + "so every query over it must be bounded in code by a tenant predicate, a specific user id, "
            + "or an id set derived from a tenant-filtered query. An unbounded query here reads across "
            + "tenants and the database will not stop it. Offending sites:\n"
            + string.Join('\n', unbounded));
    }

    /// <summary>
    /// The guard is only worth having if it can fail. This proves the rule
    /// rejects the shape it exists to catch, so a future refactor that loosens
    /// the matcher into uselessness is itself caught.
    /// </summary>
    [Theory]
    [InlineData("var all = await db.Users.ToListAsync(ct);")]
    [InlineData("return await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);")]
    [InlineData("var some = await db.Users.Where(u => u.IsActive).ToListAsync(ct);")]
    public void The_rule_rejects_an_unbounded_query(string statement)
    {
        QueryStart.IsMatch(statement).Should().BeTrue("the sample is a user-table query");
        Bounds.Any(b => b.IsMatch(statement)).Should().BeFalse(
            "an unbounded listing must not satisfy any bound");
    }

    [Theory]
    [InlineData("var users = await db.Users.Where(u => u.TenantId == tenantId).ToListAsync(ct);")]
    [InlineData("var me = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);")]
    [InlineData("var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync(ct);")]
    public void The_rule_accepts_a_bounded_query(string statement)
    {
        Bounds.Any(b => b.IsMatch(statement)).Should().BeTrue(
            "a tenant-filtered, actor-keyed or id-set-keyed query is bounded");
    }
}
