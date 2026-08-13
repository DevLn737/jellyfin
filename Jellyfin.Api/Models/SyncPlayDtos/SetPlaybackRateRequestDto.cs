using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.SyncPlayDtos;

/// <summary>
/// A request to set the base playback rate of a SyncPlay group.
/// </summary>
public class SetPlaybackRateRequestDto
{
    /// <summary>
    /// Gets or sets the requested playback rate.
    /// </summary>
    [Range(0.25, 5.0)]
    public float PlaybackRate { get; set; }
}
