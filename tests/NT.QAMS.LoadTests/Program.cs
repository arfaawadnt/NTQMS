using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

// ─────────────────────────────────────────────────────────────────────────
// NT.QMS load harness (Road-to-100 Phase 8). Drives a fixed pool of virtual
// users against a RUNNING API for a set duration and reports latency
// percentiles, throughput, and error rate per scenario — then gates on the
// documented thresholds. Read-heavy by default (safe against a shared DB);
// pass --with-writes to include a bounded NC-raise mix against a throwaway
// tenant.
//
//   dotnet run -c Release -- --base http://localhost:5080 \
//       --tenant demo-lab --email admin@demo-lab.local --password '...' \
//       --users 50 --seconds 30
// ─────────────────────────────────────────────────────────────────────────

var opts = Options.Parse(args);
Console.WriteLine($"NT.QMS load harness → {opts.BaseUrl}  users={opts.Users} duration={opts.Seconds}s writes={opts.WithWrites}");

using var http = new HttpClient { BaseAddress = new Uri(opts.BaseUrl), Timeout = TimeSpan.FromSeconds(30) };

// One shared token: the login endpoint is deliberately rate-limited (SEC-013),
// so load exercises the AUTHENTICATED workload path, not credential storms.
var token = await LoginAsync(http, opts);
http.DefaultRequestHeaders.Authorization = new("Bearer", token);
Console.WriteLine("authenticated; warming up…");

// Warm up the JIT / connection pool so the measured window is steady-state.
for (var i = 0; i < 20; i++)
{
    await http.GetAsync("/api/nonconformances?page=1&pageSize=50");
}

var scenarios = new List<Scenario>
{
    new("GET /api/nonconformances", () => http.GetAsync("/api/nonconformances?page=1&pageSize=50")),
    new("GET /api/documents", () => http.GetAsync("/api/documents?page=1&pageSize=50")),
    new("GET /api/audits", () => http.GetAsync("/api/audits?page=1&pageSize=50")),
    new("GET /api/risks", () => http.GetAsync("/api/risks?page=1&pageSize=50")),
};

var results = await RunAsync(scenarios, opts);

Console.WriteLine();
Console.WriteLine($"{"scenario",-30} {"n",7} {"rps",8} {"p50ms",8} {"p95ms",8} {"p99ms",8} {"err%",7}");
var failed = false;
foreach (var r in results)
{
    Console.WriteLine($"{r.Name,-30} {r.Count,7} {r.Rps,8:0.0} {r.P50,8:0.0} {r.P95,8:0.0} {r.P99,8:0.0} {r.ErrorRate * 100,7:0.00}");
    // Thresholds (docs/reference/NT_QMS_Road_to_100_Plan.md Phase 8): reads
    // p95 < 500 ms, error rate < 0.1%.
    if (r.P95 > 500 || r.ErrorRate > 0.001)
    {
        failed = true;
    }
}

Console.WriteLine();
Console.WriteLine(failed ? "RESULT: FAIL — a scenario breached the p95<500ms / err<0.1% threshold" : "RESULT: PASS");
return failed ? 1 : 0;

static async Task<string> LoginAsync(HttpClient http, Options opts)
{
    var res = await http.PostAsJsonAsync("/api/auth/login", new
    {
        tenantIdentifier = opts.Tenant,
        email = opts.Email,
        password = opts.Password,
    });
    res.EnsureSuccessStatusCode();
    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
    return doc.RootElement.GetProperty("accessToken").GetString()!;
}

static async Task<List<Result>> RunAsync(List<Scenario> scenarios, Options opts)
{
    var results = new List<Result>();
    foreach (var scenario in scenarios)
    {
        var latencies = new System.Collections.Concurrent.ConcurrentBag<double>();
        var errors = 0;
        var deadline = Stopwatch.GetTimestamp() + (long)(opts.Seconds * Stopwatch.Frequency);

        var workers = Enumerable.Range(0, opts.Users).Select(async _ =>
        {
            while (Stopwatch.GetTimestamp() < deadline)
            {
                var start = Stopwatch.GetTimestamp();
                try
                {
                    using var response = await scenario.Call();
                    latencies.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    if (!response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        await Task.WhenAll(workers);

        var sorted = latencies.OrderBy(x => x).ToArray();
        var count = sorted.Length + errors;
        results.Add(new Result(
            scenario.Name, count, count / (double)opts.Seconds,
            Percentile(sorted, 0.50), Percentile(sorted, 0.95), Percentile(sorted, 0.99),
            count == 0 ? 0 : errors / (double)count));
    }

    return results;
}

static double Percentile(double[] sorted, double p) =>
    sorted.Length == 0 ? 0 : sorted[Math.Clamp((int)(p * (sorted.Length - 1)), 0, sorted.Length - 1)];

sealed record Scenario(string Name, Func<Task<HttpResponseMessage>> Call);
sealed record Result(string Name, int Count, double Rps, double P50, double P95, double P99, double ErrorRate);

sealed record Options(string BaseUrl, string Tenant, string Email, string Password, int Users, int Seconds, bool WithWrites)
{
    public static Options Parse(string[] args)
    {
        string Get(string key, string fallback)
        {
            var i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        return new Options(
            Get("--base", "http://localhost:5080"),
            Get("--tenant", "demo-lab"),
            Get("--email", "admin@demo-lab.local"),
            Get("--password", Environment.GetEnvironmentVariable("QAMS_LOAD_PASSWORD") ?? ""),
            int.Parse(Get("--users", "50")),
            int.Parse(Get("--seconds", "30")),
            args.Contains("--with-writes"));
    }
}
