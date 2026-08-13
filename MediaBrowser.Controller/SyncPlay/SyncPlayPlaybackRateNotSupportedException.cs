using System;

namespace MediaBrowser.Controller.SyncPlay;

/// <summary>
/// Thrown when a group contains clients that cannot apply synchronized playback rates.
/// </summary>
public sealed class SyncPlayPlaybackRateNotSupportedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayPlaybackRateNotSupportedException"/> class.
    /// </summary>
    /// <param name="unsupportedParticipants">The number of incompatible participants.</param>
    public SyncPlayPlaybackRateNotSupportedException(int unsupportedParticipants)
        : base("One or more SyncPlay participants do not support synchronized playback rates.")
    {
        UnsupportedParticipants = unsupportedParticipants;
    }

    /// <summary>
    /// Gets the number of incompatible participants.
    /// </summary>
    public int UnsupportedParticipants { get; }
}
