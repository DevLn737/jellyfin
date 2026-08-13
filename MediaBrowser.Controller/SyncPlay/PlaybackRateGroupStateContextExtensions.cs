using System;

namespace MediaBrowser.Controller.SyncPlay;

internal static class PlaybackRateGroupStateContextExtensions
{
    internal static float GetBasePlaybackRate(this IGroupStateContext context)
        => context is IPlaybackRateGroupStateContext rateContext ? rateContext.BasePlaybackRate : 1.0F;

    internal static long ToMediaTicks(this IGroupStateContext context, TimeSpan elapsed)
        => (long)Math.Round(elapsed.Ticks * context.GetBasePlaybackRate());

    internal static long ToWallClockTicks(this IGroupStateContext context, long mediaTicks)
        => (long)Math.Round(mediaTicks / context.GetBasePlaybackRate());
}
