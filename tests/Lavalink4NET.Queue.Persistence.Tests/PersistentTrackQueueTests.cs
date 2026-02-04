namespace Lavalink4NET.Queue.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Xunit;

public sealed class PersistentTrackQueueTests
{
    private const ulong TestGuildId = 123456789UL;

    [Fact]
    public async Task AddAsync_AddsItemToQueue()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        var item = new TrackQueueItem("test-track-id");

        // Act
        var count = await queue.AddAsync(item);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(1, queue.Count);
        Assert.False(queue.IsEmpty);
    }

    [Fact]
    public async Task AddRangeAsync_AddsMultipleItemsToQueue()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        var items = new[]
        {
            new TrackQueueItem("track-1"),
            new TrackQueueItem("track-2"),
            new TrackQueueItem("track-3"),
        };

        // Act
        var count = await queue.AddRangeAsync(items);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(3, queue.Count);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllItems()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));

        // Act
        var removedCount = await queue.ClearAsync();

        // Assert
        Assert.Equal(2, removedCount);
        Assert.Equal(0, queue.Count);
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public async Task Peek_ReturnsFirstItemWithoutRemoving()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        var item = new TrackQueueItem("test-track-id");
        await queue.AddAsync(item);

        // Act
        var peeked = queue.Peek();

        // Assert
        Assert.NotNull(peeked);
        Assert.Equal("test-track-id", peeked.Identifier);
        Assert.Equal(1, queue.Count); // Still in queue
    }

    [Fact]
    public async Task TryDequeueAsync_RemovesAndReturnsFirstItem()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));

        // Act
        var dequeued = await queue.TryDequeueAsync();

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal("track-1", dequeued.Identifier);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public async Task TryDequeueAsync_ReturnsNullWhenEmpty()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);

        // Act
        var dequeued = await queue.TryDequeueAsync();

        // Assert
        Assert.Null(dequeued);
    }

    [Fact]
    public async Task RemoveAtAsync_RemovesItemAtIndex()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));
        await queue.AddAsync(new TrackQueueItem("track-3"));

        // Act
        var removed = await queue.RemoveAtAsync(1);

        // Assert
        Assert.True(removed);
        Assert.Equal(2, queue.Count);
        Assert.Equal("track-1", queue[0].Identifier);
        Assert.Equal("track-3", queue[1].Identifier);
    }

    [Fact]
    public async Task RemoveAsync_RemovesSpecificItem()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        var item1 = new TrackQueueItem("track-1");
        var item2 = new TrackQueueItem("track-2");
        await queue.AddAsync(item1);
        await queue.AddAsync(item2);

        // Act
        var removed = await queue.RemoveAsync(item1);

        // Assert
        Assert.True(removed);
        Assert.Equal(1, queue.Count);
        Assert.Equal("track-2", queue[0].Identifier);
    }

    [Fact]
    public async Task InsertAsync_InsertsAtSpecifiedIndex()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-3"));

        // Act
        await queue.InsertAsync(1, new TrackQueueItem("track-2"));

        // Assert
        Assert.Equal(3, queue.Count);
        Assert.Equal("track-1", queue[0].Identifier);
        Assert.Equal("track-2", queue[1].Identifier);
        Assert.Equal("track-3", queue[2].Identifier);
    }

    [Fact]
    public async Task ShuffleAsync_RandomizesOrder()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);

        // Add many items to make shuffle statistically likely to change order
        for (var i = 0; i < 20; i++)
        {
            await queue.AddAsync(new TrackQueueItem($"track-{i}"));
        }

        var originalOrder = new string[20];
        for (var i = 0; i < 20; i++)
        {
            originalOrder[i] = queue[i].Identifier;
        }

        // Act
        await queue.ShuffleAsync();

        // Assert - check that at least some items changed position
        var samePositionCount = 0;
        for (var i = 0; i < 20; i++)
        {
            if (queue[i].Identifier == originalOrder[i])
            {
                samePositionCount++;
            }
        }

        // With 20 items, it's extremely unlikely all positions remain the same
        Assert.True(samePositionCount < 20, "Shuffle should change at least some positions");
    }

    [Fact]
    public async Task Contains_ReturnsTrueForExistingItem()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        var item = new TrackQueueItem("test-track-id");
        await queue.AddAsync(item);

        // Act & Assert
        Assert.True(queue.Contains(item));
    }

    [Fact]
    public async Task IndexOf_ReturnsCorrectIndex()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        var item1 = new TrackQueueItem("track-1");
        var item2 = new TrackQueueItem("track-2");
        await queue.AddAsync(item1);
        await queue.AddAsync(item2);

        // Act & Assert
        Assert.Equal(0, queue.IndexOf(item1));
        Assert.Equal(1, queue.IndexOf(item2));
    }

    [Fact]
    public async Task IndexOf_WithPredicate_ReturnsCorrectIndex()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));

        // Act
        var index = queue.IndexOf(item => item.Identifier == "track-2");

        // Assert
        Assert.Equal(1, index);
    }

    [Fact]
    public async Task DistinctAsync_RemovesDuplicates()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));
        await queue.AddAsync(new TrackQueueItem("track-1")); // duplicate
        await queue.AddAsync(new TrackQueueItem("track-3"));
        await queue.AddAsync(new TrackQueueItem("track-2")); // duplicate

        // Act
        var removedCount = await queue.DistinctAsync();

        // Assert
        Assert.Equal(2, removedCount);
        Assert.Equal(3, queue.Count);
    }

    [Fact]
    public async Task RemoveAllAsync_RemovesMatchingItems()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("keep-1"));
        await queue.AddAsync(new TrackQueueItem("remove-1"));
        await queue.AddAsync(new TrackQueueItem("keep-2"));
        await queue.AddAsync(new TrackQueueItem("remove-2"));

        // Act
        var removedCount = await queue.RemoveAllAsync(item => item.Identifier.StartsWith("remove"));

        // Assert
        Assert.Equal(2, removedCount);
        Assert.Equal(2, queue.Count);
        Assert.Equal("keep-1", queue[0].Identifier);
        Assert.Equal("keep-2", queue[1].Identifier);
    }

    [Fact]
    public async Task Queue_PersistsToStore()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);

        // Act
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));

        // Verify store has the data
        var storedItems = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal(2, storedItems.Count);
    }

    [Fact]
    public async Task Queue_InitializesFromStore()
    {
        // Arrange
        var store = new InMemoryQueueStore();

        // Pre-populate store
        await store.AddAsync(new QueuedTrackModel
        {
            Id = Guid.NewGuid(),
            GuildId = TestGuildId,
            Position = 0,
            TrackData = "existing-track-1",
        });
        await store.AddAsync(new QueuedTrackModel
        {
            Id = Guid.NewGuid(),
            GuildId = TestGuildId,
            Position = 1,
            TrackData = "existing-track-2",
        });

        // Act
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.InitializeAsync();

        // Assert
        Assert.Equal(2, queue.Count);
        Assert.Equal("existing-track-1", queue[0].Identifier);
        Assert.Equal("existing-track-2", queue[1].Identifier);
    }

    [Fact]
    public async Task TryPeek_ReturnsTrueAndItemWhenNotEmpty()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("test-track"));

        // Act
        var result = queue.TryPeek(out var item);

        // Assert
        Assert.True(result);
        Assert.NotNull(item);
        Assert.Equal("test-track", item.Identifier);
    }

    [Fact]
    public void TryPeek_ReturnsFalseWhenEmpty()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);

        // Act
        var result = queue.TryPeek(out var item);

        // Assert
        Assert.False(result);
        Assert.Null(item);
    }

    [Fact]
    public async Task HasHistory_ReturnsTrueWhenHistoryEnabled()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId, historyCapacity: 10);

        // Act & Assert
        Assert.True(queue.HasHistory);
        Assert.NotNull(queue.History);
    }

    [Fact]
    public async Task HasHistory_ReturnsFalseWhenHistoryDisabled()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId, historyCapacity: null);

        // Act & Assert
        Assert.False(queue.HasHistory);
        Assert.Null(queue.History);
    }

    [Fact]
    public async Task Enumeration_ReturnsAllItems()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var queue = new PersistentTrackQueue(store, TestGuildId);
        await queue.AddAsync(new TrackQueueItem("track-1"));
        await queue.AddAsync(new TrackQueueItem("track-2"));
        await queue.AddAsync(new TrackQueueItem("track-3"));

        // Act
        var items = queue.ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("track-1", items[0].Identifier);
        Assert.Equal("track-2", items[1].Identifier);
        Assert.Equal("track-3", items[2].Identifier);
    }
}
