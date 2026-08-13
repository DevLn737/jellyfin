using System.Threading;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay.PlaybackRequests;
using MediaBrowser.Model.SyncPlay;

namespace MediaBrowser.Controller.SyncPlay;

/// <summary>
/// Handles playback-rate requests without changing the existing <see cref="IGroupState"/> ABI.
/// </summary>
public interface IPlaybackRateGroupState
{
    /// <summary>
    /// Applies a playback-rate request to a group state.
    /// </summary>
    /// <param name="request">The playback-rate request.</param>
    /// <param name="context">The group state context.</param>
    /// <param name="prevState">The previous group state.</param>
    /// <param name="session">The requesting session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    void HandleRequest(SetPlaybackRateGroupRequest request, IGroupStateContext context, GroupStateType prevState, SessionInfo session, CancellationToken cancellationToken);
}
