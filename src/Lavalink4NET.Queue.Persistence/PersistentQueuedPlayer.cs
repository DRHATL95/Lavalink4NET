namespace Lavalink4NET.Queue.Persistence;

using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     A queued player implementation that uses a persistent queue for storage.
/// </summary>
public class PersistentQueuedPlayer : QueuedLavalinkPlayer
{
    private readonly PersistentTrackQueue? _persistentQueue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PersistentQueuedPlayer"/> class.
    /// </summary>
    /// <param name="properties">The player properties.</param>
    public PersistentQueuedPlayer(IPlayerProperties<PersistentQueuedPlayer, PersistentQueuedPlayerOptions> properties)
        : base(CreateProperties(properties))
    {
        _persistentQueue = Queue as PersistentTrackQueue;
    }

    /// <summary>
    ///     Initializes the persistent queue by loading data from storage.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public async ValueTask InitializeQueueAsync(CancellationToken cancellationToken = default)
    {
        if (_persistentQueue is not null)
        {
            await _persistentQueue.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static IPlayerProperties<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> CreateProperties(
        IPlayerProperties<PersistentQueuedPlayer, PersistentQueuedPlayerOptions> properties)
    {
        var factory = properties.ServiceProvider?.GetService<IPersistentQueueFactory>();
        var queue = factory?.Create(properties.InitialState.GuildId);

        return new PlayerPropertiesAdapter(properties, queue);
    }

    private sealed class PlayerPropertiesAdapter : IPlayerProperties<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>
    {
        private readonly IPlayerProperties<PersistentQueuedPlayer, PersistentQueuedPlayerOptions> _inner;
        private readonly QueuedLavalinkPlayerOptions _options;

        public PlayerPropertiesAdapter(
            IPlayerProperties<PersistentQueuedPlayer, PersistentQueuedPlayerOptions> inner,
            ITrackQueue? queue)
        {
            _inner = inner;

            // Create adapted options with the persistent queue
            var originalOptions = inner.Options.Value;
            _options = new QueuedLavalinkPlayerOptions
            {
                TrackQueue = queue ?? originalOptions.TrackQueue,
                HistoryCapacity = originalOptions.HistoryCapacity,
                EnableAutoPlay = originalOptions.EnableAutoPlay,
                ClearQueueOnStop = originalOptions.ClearQueueOnStop,
                ClearHistoryOnStop = originalOptions.ClearHistoryOnStop,
                ResetTrackRepeatOnStop = originalOptions.ResetTrackRepeatOnStop,
                ResetShuffleOnStop = originalOptions.ResetShuffleOnStop,
                HistoryBehavior = originalOptions.HistoryBehavior,
                RespectTrackRepeatOnSkip = originalOptions.RespectTrackRepeatOnSkip,
                DefaultTrackRepeatMode = originalOptions.DefaultTrackRepeatMode,
                SelfDeaf = originalOptions.SelfDeaf,
                SelfMute = originalOptions.SelfMute,
                InitialVolume = originalOptions.InitialVolume,
            };
        }

        public Rest.ILavalinkApiClient ApiClient => _inner.ApiClient;
        public Clients.IDiscordClientWrapper DiscordClient => _inner.DiscordClient;
        public Protocol.Models.PlayerInformationModel InitialState => _inner.InitialState;
        public ITrackQueueItem? InitialTrack => _inner.InitialTrack;
        public string Label => _inner.Label;
        public Microsoft.Extensions.Logging.ILogger<QueuedLavalinkPlayer> Logger =>
            (Microsoft.Extensions.Logging.ILogger<QueuedLavalinkPlayer>)_inner.Logger;
        public ISystemClock SystemClock => _inner.SystemClock;
        public Microsoft.Extensions.Options.IOptions<QueuedLavalinkPlayerOptions> Options =>
            Microsoft.Extensions.Options.Options.Create(_options);
        public System.IServiceProvider? ServiceProvider => _inner.ServiceProvider;
        public ulong VoiceChannelId => _inner.VoiceChannelId;
        public string SessionId => _inner.SessionId;
        public IPlayerLifecycle Lifecycle => _inner.Lifecycle;
    }
}

/// <summary>
///     Options for the persistent queued player.
/// </summary>
public record class PersistentQueuedPlayerOptions : QueuedLavalinkPlayerOptions
{
}
