namespace NT.QAMS.SharedKernel.Abstractions;

/// <summary>
/// Clock abstraction. Domain and application code never call DateTime/DateTimeOffset
/// statics directly — testability, and record-truth timestamps stay controllable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
