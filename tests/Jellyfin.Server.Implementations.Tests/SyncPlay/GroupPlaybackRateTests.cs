using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.SyncPlay;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay;
using MediaBrowser.Controller.SyncPlay.GroupStates;
using MediaBrowser.Controller.SyncPlay.PlaybackRequests;
using MediaBrowser.Controller.SyncPlay.Requests;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.SyncPlay;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.SyncPlay;

public class GroupPlaybackRateTests
{
    private readonly Mock<ILoggerFactory> _loggerFactory;
    private readonly Mock<ISessionManager> _sessionManager;
    private readonly List<SessionInfo> _sessions = [];
    private readonly List<SendCommand> _commands = [];

    public GroupPlaybackRateTests()
    {
        _loggerFactory = new Mock<ILoggerFactory>();
        _loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        _sessionManager = new Mock<ISessionManager>();
        _sessionManager.SetupGet(manager => manager.Sessions).Returns(_sessions);
        _sessionManager.SetReturnsDefault(Task.CompletedTask);
        _sessionManager
            .Setup(manager => manager.SendSyncPlayCommand(It.IsAny<string>(), It.IsAny<SendCommand>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendCommand, CancellationToken>((_, command, _) => _commands.Add(command))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void LegacyConstructors_DefaultPlaybackRateToOne()
    {
        var command = new SendCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, SendCommandType.Pause, 0, DateTime.UtcNow);
        var update = new PlayQueueUpdate(
            PlayQueueUpdateReason.NewPlaylist,
            DateTime.UtcNow,
            [],
            0,
            0,
            false,
            GroupShuffleMode.Sorted,
            GroupRepeatMode.RepeatNone);

        Assert.Equal(1.0F, command.PlaybackRate);
        Assert.Equal(1.0F, update.PlaybackRate);
    }

    [Fact]
    public void LegacyCapabilitiesPayload_DefaultsPlaybackRateSupportToFalse()
    {
        var capabilities = JsonSerializer.Deserialize<ClientCapabilities>("{}");

        Assert.NotNull(capabilities);
        Assert.False(capabilities.SupportsSyncPlayPlaybackRate);
    }

    [Fact]
    public void SetPlaybackRate_InIdleState_PersistsAndBroadcasts()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: true);

        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.5F), CancellationToken.None);

        Assert.Equal(1.5F, group.BasePlaybackRate);
        var command = Assert.Single(_commands, item => item.PlaybackRate == 1.5F);
        Assert.Equal(SendCommandType.Stop, command.Command);
    }

    [Fact]
    public void SetPlaybackRate_WithLegacyParticipant_LeavesGroupUnchanged()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: false);

        var exception = Assert.Throws<SyncPlayPlaybackRateNotSupportedException>(
            () => group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.25F), CancellationToken.None));

        Assert.Equal(1, exception.UnsupportedParticipants);
        Assert.Equal(1.0F, group.BasePlaybackRate);
    }

    [Fact]
    public void SetPlaybackRate_ReturnToOne_IsAllowedWithLegacyParticipant()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: true);
        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.5F), CancellationToken.None);
        session.Capabilities.SupportsSyncPlayPlaybackRate = false;

        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.0F), CancellationToken.None);

        Assert.Equal(1.0F, group.BasePlaybackRate);
    }

    [Fact]
    public void SetPlaybackRate_WhilePaused_DoesNotResumePlayback()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: true);
        group.PositionTicks = 0;
        group.SetState(new PausedGroupState(_loggerFactory.Object));
        _commands.Clear();

        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.25F), CancellationToken.None);
        group.HandleRequest(
            session,
            new ReadyGroupRequest(DateTime.UtcNow, group.PositionTicks, false, Guid.Empty),
            CancellationToken.None);

        Assert.Equal(1.25F, group.BasePlaybackRate);
        Assert.Equal(GroupStateType.Paused, group.GetInfo().State);
        Assert.DoesNotContain(_commands, item => item.Command == SendCommandType.Unpause);
    }

    [Fact]
    public void SetPlaybackRate_WhilePlaying_AnchorsWithPreviousRate()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: true);
        group.BasePlaybackRate = 1.25F;
        group.PositionTicks = TimeSpan.FromSeconds(10).Ticks;
        group.LastActivity = DateTime.UtcNow.AddSeconds(-2);
        group.SetState(new PlayingGroupState(_loggerFactory.Object));
        _commands.Clear();

        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.5F), CancellationToken.None);

        Assert.InRange(
            group.PositionTicks,
            TimeSpan.FromSeconds(12.3).Ticks,
            TimeSpan.FromSeconds(12.8).Ticks);
        Assert.Equal(1.5F, group.BasePlaybackRate);
        var command = Assert.Single(_commands, item => item.Command == SendCommandType.Seek);
        Assert.Equal(1.5F, command.PlaybackRate);
    }

    [Fact]
    public void SetPlaybackRate_WithOutOfRangePlayingReady_SeeksInsteadOfSchedulingDistantUnpause()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: true);
        group.PositionTicks = TimeSpan.FromSeconds(228).Ticks;
        group.LastActivity = DateTime.UtcNow;
        group.SetState(new PlayingGroupState(_loggerFactory.Object));

        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.5F), CancellationToken.None);
        _commands.Clear();
        group.HandleRequest(
            session,
            new ReadyGroupRequest(DateTime.UtcNow, TimeSpan.FromMilliseconds(228).Ticks, true, Guid.Empty),
            CancellationToken.None);

        Assert.Equal(GroupStateType.Waiting, group.GetInfo().State);
        var command = Assert.Single(_commands, item => item.Command == SendCommandType.Seek);
        Assert.Equal(group.PositionTicks, command.PositionTicks);
        Assert.DoesNotContain(_commands, item => item.Command == SendCommandType.Unpause);
    }

    [Fact]
    public void AcceleratedGroup_RejectsSessionWithoutCapability()
    {
        var (group, session) = CreateGroup(supportsPlaybackRate: true);
        group.HandleRequest(session, new SetPlaybackRateGroupRequest(1.5F), CancellationToken.None);
        var legacySession = CreateSession("legacy", supportsPlaybackRate: false);

        Assert.Throws<SyncPlayPlaybackRateNotSupportedException>(() => group.EnsureSessionCanJoin(legacySession));
    }

    [Fact]
    public void PlaybackRate_PersistsAfterInitiatorLeaves()
    {
        var (group, initiator) = CreateGroup(supportsPlaybackRate: true);
        var remainingSession = CreateSession("remaining", supportsPlaybackRate: true);
        _sessions.Add(remainingSession);
        group.SessionJoin(remainingSession, new JoinGroupRequest(group.GroupId), CancellationToken.None);

        group.HandleRequest(initiator, new SetPlaybackRateGroupRequest(1.5F), CancellationToken.None);
        group.SessionLeave(initiator, new LeaveGroupRequest(), CancellationToken.None);

        Assert.Equal(1.5F, group.BasePlaybackRate);
        Assert.Equal(1.5F, group.GetPlayQueueUpdate(PlayQueueUpdateReason.Queue).PlaybackRate);
    }

    [Theory]
    [InlineData(0.25F)]
    [InlineData(1.0F)]
    [InlineData(1.25F)]
    [InlineData(1.5F)]
    [InlineData(5.0F)]
    public void PlaybackRateRequest_AcceptsSupportedValues(float playbackRate)
    {
        var request = new SetPlaybackRateGroupRequest(playbackRate);

        Assert.Equal(playbackRate, request.PlaybackRate);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(0.24F)]
    [InlineData(5.01F)]
    public void PlaybackRateRequest_RejectsInvalidValues(float playbackRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SetPlaybackRateGroupRequest(playbackRate));
    }

    private (Group Group, SessionInfo Session) CreateGroup(bool supportsPlaybackRate)
    {
        var session = CreateSession("session", supportsPlaybackRate);
        _sessions.Add(session);

        var group = new Group(
            _loggerFactory.Object,
            Mock.Of<IUserManager>(),
            _sessionManager.Object,
            Mock.Of<ILibraryManager>());
        group.CreateGroup(session, new NewGroupRequest("test"), CancellationToken.None);
        return (group, session);
    }

    private SessionInfo CreateSession(string id, bool supportsPlaybackRate)
    {
        return new SessionInfo(_sessionManager.Object, Mock.Of<ILogger>())
        {
            Id = id,
            UserName = id,
            Capabilities = new ClientCapabilities
            {
                SupportsSyncPlayPlaybackRate = supportsPlaybackRate
            }
        };
    }
}
