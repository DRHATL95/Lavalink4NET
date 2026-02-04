namespace Lavalink4NET.Queue.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Xunit;

public sealed class QueueStoreTests
{
    private const ulong TestGuildId = 123456789UL;

    [Fact]
    public async Task GetQueueAsync_ReturnsEmptyListForNewGuild()
    {
        // Arrange
        var store = new InMemoryQueueStore();

        // Act
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task AddAsync_AddsItemToStore()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var model = CreateModel(position: 0);

        // Act
        await store.AddAsync(model);
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Single(items);
        Assert.Equal(model.Id, items[0].Id);
    }

    [Fact]
    public async Task AddRangeAsync_AddsMultipleItems()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var models = new[]
        {
            CreateModel(position: 0),
            CreateModel(position: 1),
            CreateModel(position: 2),
        };

        // Act
        await store.AddRangeAsync(models);
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task RemoveAsync_RemovesItemById()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var model = CreateModel(position: 0);
        await store.AddAsync(model);

        // Act
        var result = await store.RemoveAsync(TestGuildId, model.Id);
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.True(result);
        Assert.Empty(items);
    }

    [Fact]
    public async Task RemoveAsync_ReturnsFalseForNonexistentItem()
    {
        // Arrange
        var store = new InMemoryQueueStore();

        // Act
        var result = await store.RemoveAsync(TestGuildId, Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllItems()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        await store.AddAsync(CreateModel(position: 0));
        await store.AddAsync(CreateModel(position: 1));

        // Act
        var count = await store.ClearAsync(TestGuildId);
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal(2, count);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        await store.AddAsync(CreateModel(position: 0));
        await store.AddAsync(CreateModel(position: 1));
        await store.AddAsync(CreateModel(position: 2));

        // Act
        var count = await store.GetCountAsync(TestGuildId);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task PeekAsync_ReturnsFirstItemWithoutRemoving()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var model = CreateModel(position: 0, trackData: "first-track");
        await store.AddAsync(model);
        await store.AddAsync(CreateModel(position: 1, trackData: "second-track"));

        // Act
        var peeked = await store.PeekAsync(TestGuildId);
        var count = await store.GetCountAsync(TestGuildId);

        // Assert
        Assert.NotNull(peeked);
        Assert.Equal("first-track", peeked.TrackData);
        Assert.Equal(2, count); // Still 2 items
    }

    [Fact]
    public async Task DequeueAsync_RemovesAndReturnsFirstItem()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        await store.AddAsync(CreateModel(position: 0, trackData: "first-track"));
        await store.AddAsync(CreateModel(position: 1, trackData: "second-track"));

        // Act
        var dequeued = await store.DequeueAsync(TestGuildId);
        var count = await store.GetCountAsync(TestGuildId);

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal("first-track", dequeued.TrackData);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetQueueAsync_ReturnsItemsOrderedByPosition()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        // Add in non-sequential order
        await store.AddAsync(CreateModel(position: 2, trackData: "track-3"));
        await store.AddAsync(CreateModel(position: 0, trackData: "track-1"));
        await store.AddAsync(CreateModel(position: 1, trackData: "track-2"));

        // Act
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal("track-1", items[0].TrackData);
        Assert.Equal("track-2", items[1].TrackData);
        Assert.Equal("track-3", items[2].TrackData);
    }

    [Fact]
    public async Task InsertAsync_ShiftsExistingPositions()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        await store.AddAsync(CreateModel(position: 0, trackData: "track-1"));
        await store.AddAsync(CreateModel(position: 1, trackData: "track-3"));

        // Act
        await store.InsertAsync(CreateModel(position: 1, trackData: "track-2"));
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("track-1", items[0].TrackData);
        Assert.Equal("track-2", items[1].TrackData);
        Assert.Equal("track-3", items[2].TrackData);
    }

    [Fact]
    public async Task RemoveRangeAsync_RemovesItemsInRange()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        await store.AddAsync(CreateModel(position: 0, trackData: "track-0"));
        await store.AddAsync(CreateModel(position: 1, trackData: "track-1"));
        await store.AddAsync(CreateModel(position: 2, trackData: "track-2"));
        await store.AddAsync(CreateModel(position: 3, trackData: "track-3"));
        await store.AddAsync(CreateModel(position: 4, trackData: "track-4"));

        // Act
        await store.RemoveRangeAsync(TestGuildId, startPosition: 1, count: 3);
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("track-0", items[0].TrackData);
        Assert.Equal("track-4", items[1].TrackData);
    }

    [Fact]
    public async Task UpdatePositionsAsync_UpdatesItemPositions()
    {
        // Arrange
        var store = new InMemoryQueueStore();
        var model1 = CreateModel(position: 0, trackData: "track-1");
        var model2 = CreateModel(position: 1, trackData: "track-2");
        var model3 = CreateModel(position: 2, trackData: "track-3");
        await store.AddAsync(model1);
        await store.AddAsync(model2);
        await store.AddAsync(model3);

        // Act - reverse the order
        await store.UpdatePositionsAsync(TestGuildId, new[] { model3.Id, model2.Id, model1.Id });
        var items = await store.GetQueueAsync(TestGuildId);

        // Assert
        Assert.Equal("track-3", items[0].TrackData);
        Assert.Equal("track-2", items[1].TrackData);
        Assert.Equal("track-1", items[2].TrackData);
    }

    private static QueuedTrackModel CreateModel(int position, string trackData = "test-track")
    {
        return new QueuedTrackModel
        {
            Id = Guid.NewGuid(),
            GuildId = TestGuildId,
            Position = position,
            TrackData = trackData,
            AddedAt = DateTimeOffset.UtcNow,
        };
    }
}
