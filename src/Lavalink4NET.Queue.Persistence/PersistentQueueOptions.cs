namespace Lavalink4NET.Queue.Persistence;

/// <summary>
///     Options for configuring persistent track queues.
/// </summary>
public sealed class PersistentQueueOptions
{
    /// <summary>
    ///     Gets or sets the history capacity for each queue.
    ///     Set to null to disable history.
    /// </summary>
    public int? HistoryCapacity { get; set; } = 8;

    /// <summary>
    ///     Gets or sets whether to automatically initialize the queue from storage
    ///     when the player is created.
    /// </summary>
    public bool AutoInitialize { get; set; } = true;
}
