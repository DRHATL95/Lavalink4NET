namespace Lavalink4NET.Queue.EntityFrameworkCore.Extensions;

using System;
using Microsoft.EntityFrameworkCore;

/// <summary>
///     Extension methods for configuring the queue entity in Entity Framework Core.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Configures the queued tracks entity with optimal settings.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="tableName">Optional custom table name (default: "QueuedTracks").</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ConfigureQueuedTracks(
        this ModelBuilder modelBuilder,
        string tableName = "QueuedTracks")
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<QueuedTrackEntity>(entity =>
        {
            entity.ToTable(tableName);

            entity.HasKey(e => e.Id);

            // Index for efficient guild-based queries
            entity.HasIndex(e => new { e.GuildId, e.Position })
                .HasDatabaseName("IX_QueuedTracks_GuildId_Position");

            // Index for efficient guild lookups
            entity.HasIndex(e => e.GuildId)
                .HasDatabaseName("IX_QueuedTracks_GuildId");

            // Configure properties
            entity.Property(e => e.GuildId)
                .IsRequired();

            entity.Property(e => e.Position)
                .IsRequired();

            entity.Property(e => e.TrackData)
                .IsRequired()
                .HasMaxLength(4000); // Track data is typically ~500-1000 chars

            entity.Property(e => e.AddedAt)
                .IsRequired();

            entity.Property(e => e.Metadata)
                .HasMaxLength(2000);
        });

        return modelBuilder;
    }
}
