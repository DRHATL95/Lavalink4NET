namespace Lavalink4NET.Queue.Persistence.Tests;

using System;
using Lavalink4NET.Players.Queued;
using Xunit;

public sealed class DefaultQueueItemSerializerTests
{
    private const ulong TestGuildId = 123456789UL;

    [Fact]
    public void Serialize_CreatesValidModel()
    {
        // Arrange
        var serializer = DefaultQueueItemSerializer.Instance;
        var item = new TrackQueueItem("test-track-identifier");

        // Act
        var model = serializer.Serialize(item, TestGuildId, position: 5);

        // Assert
        Assert.NotEqual(Guid.Empty, model.Id);
        Assert.Equal(TestGuildId, model.GuildId);
        Assert.Equal(5, model.Position);
        Assert.Equal("test-track-identifier", model.TrackData);
        Assert.True(model.AddedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Deserialize_CreatesValidTrackQueueItem()
    {
        // Arrange
        var serializer = DefaultQueueItemSerializer.Instance;
        var model = new QueuedTrackModel
        {
            Id = Guid.NewGuid(),
            GuildId = TestGuildId,
            Position = 0,
            TrackData = "my-track-id",
        };

        // Act
        var item = serializer.Deserialize(model);

        // Assert
        Assert.NotNull(item);
        Assert.Equal("my-track-id", item.Identifier);
    }

    [Fact]
    public void RoundTrip_PreservesIdentifier()
    {
        // Arrange
        var serializer = DefaultQueueItemSerializer.Instance;
        var originalItem = new TrackQueueItem("round-trip-test");

        // Act
        var model = serializer.Serialize(originalItem, TestGuildId, 0);
        var deserializedItem = serializer.Deserialize(model);

        // Assert
        Assert.Equal(originalItem.Identifier, deserializedItem.Identifier);
    }

    [Fact]
    public void Serialize_ThrowsOnNullItem()
    {
        // Arrange
        var serializer = DefaultQueueItemSerializer.Instance;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null!, TestGuildId, 0));
    }

    [Fact]
    public void Deserialize_ThrowsOnNullModel()
    {
        // Arrange
        var serializer = DefaultQueueItemSerializer.Instance;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null!));
    }
}
