namespace Resource_Management_System.Services;

public static class QueueAudience
{
    public const string Review = "review";
    public const string Approver = "approver";
    public const string Issue = "issue";
    public const string MyRequests = "myrequests";
}

/// <summary>
/// Publishes version changes only to the queue audiences affected by a
/// workflow action. This prevents an IT submission from making every open
/// approver and issuance page query the database at the same time.
/// </summary>
public sealed class QueueUpdateNotifier
{
    private sealed class AudienceState
    {
        public long Version { get; set; }
        public TaskCompletionSource<long> Changed { get; set; } = CreateSource();
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, AudienceState> _states =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [QueueAudience.Review] = new AudienceState(),
            [QueueAudience.Approver] = new AudienceState(),
            [QueueAudience.Issue] = new AudienceState(),
            [QueueAudience.MyRequests] = new AudienceState()
        };

    public long GetCurrentVersion(string audience)
    {
        lock (_sync)
        {
            return GetState(audience).Version;
        }
    }

    public Task<long> WaitForChangeAsync(
        string audience,
        long observedVersion,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var state = GetState(audience);

            if (state.Version != observedVersion)
            {
                return Task.FromResult(state.Version);
            }

            return state.Changed.Task.WaitAsync(cancellationToken);
        }
    }

    public void Notify(params string[] audiences)
    {
        var completed = new List<(TaskCompletionSource<long> Source, long Version)>();

        lock (_sync)
        {
            foreach (var audience in audiences.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var state = GetState(audience);
                state.Version++;
                completed.Add((state.Changed, state.Version));
                state.Changed = CreateSource();
            }
        }

        foreach (var item in completed)
        {
            item.Source.TrySetResult(item.Version);
        }
    }

    private AudienceState GetState(string audience) =>
        _states.TryGetValue(audience, out var state)
            ? state
            : _states[QueueAudience.Review];

    private static TaskCompletionSource<long> CreateSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
