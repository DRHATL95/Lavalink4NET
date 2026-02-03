namespace Lavalink4NET.Queue.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

/// <summary>
///     Interface for a DbContext that includes the queued tracks table.
/// </summary>
public interface IQueueDbContext
{
    /// <summary>
    ///     Gets the set of queued tracks.
    /// </summary>
    DbSet<QueuedTrackEntity> QueuedTracks { get; }

    /// <summary>
    ///     Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default);
}
