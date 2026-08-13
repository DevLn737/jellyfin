using System;
using System.Threading;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.SyncPlay;

namespace MediaBrowser.Controller.SyncPlay.PlaybackRequests;

/// <summary>
/// Requests a new server-owned base playback rate for a SyncPlay group.
/// </summary>
public class SetPlaybackRateGroupRequest : AbstractPlaybackRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetPlaybackRateGroupRequest"/> class.
    /// </summary>
    /// <param name="playbackRate">The requested base playback rate.</param>
    public SetPlaybackRateGroupRequest(float playbackRate)
    {
        if (float.IsNaN(playbackRate) || float.IsInfinity(playbackRate) || playbackRate < 0.25F || playbackRate > 5.0F)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
        }

        PlaybackRate = playbackRate;
    }

    /// <summary>
    /// Gets the requested base playback rate.
    /// </summary>
    public float PlaybackRate { get; }

    /// <inheritdoc />
    public override PlaybackRequestType Action { get; } = PlaybackRequestType.SetPlaybackRate;

    /// <inheritdoc />
    public override void Apply(IGroupStateContext context, IGroupState state, SessionInfo session, CancellationToken cancellationToken)
    {
        if (state is IPlaybackRateGroupState playbackRateState)
        {
            playbackRateState.HandleRequest(this, context, state.Type, session, cancellationToken);
            return;
        }

        state.HandleRequest(this, context, state.Type, session, cancellationToken);
    }
}
