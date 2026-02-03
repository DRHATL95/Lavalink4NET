namespace Lavalink4NET.Queue.Redis;

/// <summary>
///     Options for configuring the Redis queue store.
/// </summary>
public sealed class RedisQueueStoreOptions
{
    /// <summary>
    ///     Gets or sets the Redis connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    ///     Gets or sets the key prefix for queue data.
    ///     Default is "lavalink4net:queue:".
    /// </summary>
    public string KeyPrefix { get; set; } = "lavalink4net:queue:";

    /// <summary>
    ///     Gets or sets the Redis database index to use.
    ///     Default is -1 (use default database).
    /// </summary>
    public int DatabaseIndex { get; set; } = -1;
}
