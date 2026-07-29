namespace NT.QAMS.WebApi.Startup;

/// <summary>
/// OPS-010: shared flag telling <see cref="DeferredStartupSeeder"/> whether the
/// pre-listen seeding attempt already succeeded. Written once during startup and
/// read once when the background service starts, so a plain volatile field is
/// sufficient.
/// </summary>
public sealed class StartupSeedingState
{
    private volatile bool _completed;

    /// <summary>True once the seeding steps have run to completion.</summary>
    public bool Completed
    {
        get => _completed;
        set => _completed = value;
    }
}
