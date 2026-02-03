// This sample demonstrates how to use persistent queues with Lavalink4NET.
// The queue state is persisted to Redis, allowing you to:
// - Recover queue state after bot restarts
// - Access queue data from a web dashboard
// - Share queue state across multiple bot instances

using Discord;
using Discord.WebSocket;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Queue.Persistence;
using Lavalink4NET.Queue.Persistence.Extensions;
using Lavalink4NET.Queue.Redis.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure Discord client
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<IDiscordClient>(sp => sp.GetRequiredService<DiscordSocketClient>());

// Configure Lavalink4NET
builder.Services.AddLavalink();

// Configure Redis-backed persistent queue
// This stores queue state in Redis, enabling:
// - Queue recovery after bot restarts
// - Dashboard access to queue data
// - Cross-instance queue sharing
builder.Services.AddRedisPersistentQueue(
    connectionString: builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379",
    configurePersistence: options =>
    {
        options.HistoryCapacity = 10; // Keep last 10 played tracks
        options.AutoInitialize = true; // Load queue from Redis on player creation
    });

// Register the persistent queue player factory
builder.Services.AddSingleton<PersistentQueuePlayerFactory>();

// Add sample commands
builder.Services.AddHostedService<BotService>();

var app = builder.Build();
await app.RunAsync();

// Bot service to handle Discord connection
file sealed class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BotService> _logger;

    public BotService(DiscordSocketClient client, ILogger<BotService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += message =>
        {
            _logger.LogInformation("{Message}", message.Message);
            return Task.CompletedTask;
        };

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("DISCORD_TOKEN environment variable not set. Bot will not connect.");
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }
}

// Factory for creating players with persistent queues
file sealed class PersistentQueuePlayerFactory
{
    private readonly IPersistentQueueFactory _queueFactory;

    public PersistentQueuePlayerFactory(IPersistentQueueFactory queueFactory)
    {
        _queueFactory = queueFactory;
    }

    // Use this factory when creating players to get persistent queue support
    // Example usage in a slash command:
    //
    // var player = await audioService.Players.RetrieveAsync(
    //     Context.Guild.Id,
    //     Context.User.VoiceChannel.Id,
    //     PlayerFactory.Create<PersistentQueuedPlayer, PersistentQueuedPlayerOptions>(CreatePlayer));
    //
    // ValueTask<PersistentQueuedPlayer> CreatePlayer(IPlayerProperties<PersistentQueuedPlayer, PersistentQueuedPlayerOptions> properties)
    // {
    //     return ValueTask.FromResult(new PersistentQueuedPlayer(properties));
    // }
}
