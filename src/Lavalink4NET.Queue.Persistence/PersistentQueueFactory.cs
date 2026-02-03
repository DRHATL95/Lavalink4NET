namespace Lavalink4NET.Queue.Persistence;

using System;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Factory for creating persistent track queues.
/// </summary>
public sealed class PersistentQueueFactory : IPersistentQueueFactory
{
    private readonly IQueueStore _store;
    private readonly IQueueItemSerializer _serializer;
    private readonly IOptions<PersistentQueueOptions> _options;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PersistentQueueFactory"/> class.
    /// </summary>
    /// <param name="store">The queue store to use.</param>
    /// <param name="options">The persistent queue options.</param>
    /// <param name="serializer">The optional custom serializer.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    public PersistentQueueFactory(
        IQueueStore store,
        IOptions<PersistentQueueOptions> options,
        IQueueItemSerializer? serializer = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _options = options;
        _serializer = serializer ?? DefaultQueueItemSerializer.Instance;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public ITrackQueue Create(ulong guildId)
    {
        var logger = _loggerFactory?.CreateLogger<PersistentTrackQueue>();

        return new PersistentTrackQueue(
            _store,
            guildId,
            _serializer,
            _options.Value.HistoryCapacity,
            logger);
    }
}

/// <summary>
///     Factory interface for creating persistent track queues.
/// </summary>
public interface IPersistentQueueFactory
{
    /// <summary>
    ///     Creates a new persistent track queue for the specified guild.
    /// </summary>
    /// <param name="guildId">The guild ID that will own the queue.</param>
    /// <returns>A new persistent track queue instance.</returns>
    ITrackQueue Create(ulong guildId);
}
