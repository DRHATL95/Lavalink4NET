namespace Lavalink4NET.Queue.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
///     Entity representing a queued track in the database.
/// </summary>
[Table("QueuedTracks")]
public class QueuedTrackEntity
{
    /// <summary>
    ///     Gets or sets the unique identifier for this queue item.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID that owns this queue item.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the position in the queue (0-based index).
    /// </summary>
    [Required]
    public int Position { get; set; }

    /// <summary>
    ///     Gets or sets the encoded track data (base64 Lavalink track format).
    /// </summary>
    [Required]
    public string TrackData { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the timestamp when this item was added to the queue.
    /// </summary>
    [Required]
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>
    ///     Gets or sets optional metadata associated with this queue item.
    /// </summary>
    public string? Metadata { get; set; }
}
