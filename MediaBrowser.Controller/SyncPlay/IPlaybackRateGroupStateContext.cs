namespace MediaBrowser.Controller.SyncPlay;

/// <summary>
/// Playback-rate state kept separately from the existing <see cref="IGroupStateContext"/> ABI.
/// </summary>
public interface IPlaybackRateGroupStateContext
{
    /// <summary>
    /// Gets or sets the server-owned base playback rate.
    /// </summary>
    float BasePlaybackRate { get; set; }

    /// <summary>
    /// Sanitizes a playback rate received from an internal caller.
    /// </summary>
    /// <param name="playbackRate">The playback rate to sanitize.</param>
    /// <returns>A valid playback rate.</returns>
    float SanitizePlaybackRate(float playbackRate);
}
