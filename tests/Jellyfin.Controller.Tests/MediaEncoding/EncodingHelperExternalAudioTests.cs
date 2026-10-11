using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using IConfigurationManager = MediaBrowser.Common.Configuration.IConfigurationManager;

namespace Jellyfin.Controller.Tests.MediaEncoding;

public class EncodingHelperExternalAudioTests
{
    [Fact]
    public void AttachMediaSourceInfo_AudioRequest_SelectsRequestedExternalStream()
    {
        var helper = CreateEncodingHelper();
        var (state, mediaSource, externalAudio) = CreateExternalAudioState();

        helper.AttachMediaSourceInfo(state, new EncodingOptions(), mediaSource, "stream.ac3");

        Assert.Same(externalAudio, state.AudioStream);
    }

    [Fact]
    public void GetProgressiveAudioFullCommandLine_ExternalAudio_MapsExternalInput()
    {
        var (helper, mediaEncoder) = CreateEncodingHelperWithMediaEncoder();
        var (state, mediaSource, _) = CreateExternalAudioState();
        mediaEncoder
            .Setup(encoder => encoder.GetInputPathArgument(state))
            .Returns("\"episode.mkv\"");

        helper.AttachMediaSourceInfo(state, new EncodingOptions(), mediaSource, "stream.ac3");
        var commandLine = helper.GetProgressiveAudioFullCommandLine(state, new EncodingOptions(), "output.ac3");

        Assert.Contains("-i \"episode.mkv\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("-i \"episode.ru.mka\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("-map 1:0", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("-map 0:1", commandLine, StringComparison.Ordinal);
        Assert.Contains("-vn", commandLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public void AttachMediaSourceInfo_InternalOrDefaultAudio_DoesNotSelectSidecar(int? audioStreamIndex)
    {
        var (helper, mediaEncoder) = CreateEncodingHelperWithMediaEncoder();
        var (state, mediaSource, _) = CreateExternalAudioState();
        state.BaseRequest.AudioStreamIndex = audioStreamIndex;
        mediaEncoder.Setup(encoder => encoder.GetInputPathArgument(state)).Returns("\"episode.mkv\"");

        helper.AttachMediaSourceInfo(state, new EncodingOptions(), mediaSource, "stream.ac3");
        var commandLine = helper.GetProgressiveAudioFullCommandLine(state, new EncodingOptions(), "output.ac3");

        Assert.Equal(1, state.AudioStream.Index);
        Assert.False(state.AudioStream.IsExternal);
        Assert.Contains("-map 0:1", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("episode.ru.mka", commandLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetProgressiveAudioFullCommandLine_UnknownMediaInfo_AlwaysExcludesVideo(bool unknownAudioIndex)
    {
        var (helper, mediaEncoder) = CreateEncodingHelperWithMediaEncoder();
        var (state, mediaSource, _) = CreateExternalAudioState();
        state.MediaSource = mediaSource;
        state.IsInputVideo = unknownAudioIndex;
        state.AudioStream = unknownAudioIndex
            ? new MediaStream { Type = MediaStreamType.Audio, Index = -1, Codec = "aac", Channels = 2 }
            : null;
        mediaSource.MediaStreams = state.AudioStream is null ? [] : [state.AudioStream];
        mediaEncoder.Setup(encoder => encoder.GetInputPathArgument(state)).Returns("\"episode.mkv\"");

        var commandLine = helper.GetProgressiveAudioFullCommandLine(state, new EncodingOptions(), "output.ac3");

        Assert.Contains("-vn", commandLine, StringComparison.Ordinal);
    }

    private static EncodingHelper CreateEncodingHelper()
        => CreateEncodingHelperWithMediaEncoder().Helper;

    private static (EncodingHelper Helper, Mock<IMediaEncoder> MediaEncoder) CreateEncodingHelperWithMediaEncoder()
    {
        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.SetupGet(encoder => encoder.EncoderVersion).Returns(new Version(8, 0));

        var helper = new EncodingHelper(
            Mock.Of<IApplicationPaths>(),
            mediaEncoder.Object,
            Mock.Of<ISubtitleEncoder>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<IConfigurationManager>(),
            Mock.Of<IPathManager>());

        return (helper, mediaEncoder);
    }

    private static (EncodingJobInfo State, MediaSourceInfo MediaSource, MediaStream ExternalAudio) CreateExternalAudioState()
    {
        var sourcePath = "episode.mkv";
        var externalAudio = new MediaStream
        {
            Type = MediaStreamType.Audio,
            Index = 2,
            Path = "episode.ru.mka",
            Codec = "aac",
            Channels = 2,
            IsExternal = true
        };

        var mediaSource = new MediaSourceInfo
        {
            Path = sourcePath,
            Protocol = MediaProtocol.File,
            Container = "mkv",
            MediaStreams = new List<MediaStream>
            {
                new()
                {
                    Type = MediaStreamType.Video,
                    Index = 0,
                    Path = sourcePath,
                    Codec = "h264"
                },
                new()
                {
                    Type = MediaStreamType.Audio,
                    Index = 1,
                    Path = sourcePath,
                    Codec = "aac",
                    Channels = 2
                },
                externalAudio
            }
        };

        var state = new EncodingJobInfo(TranscodingJobType.Progressive)
        {
            BaseRequest = new BaseEncodingJobOptions
            {
                AudioStreamIndex = externalAudio.Index,
                AudioCodec = "ac3",
                Container = "ac3"
            },
            IsInputVideo = true,
            IsVideoRequest = false,
            OutputAudioBitrate = 256_000,
            OutputAudioChannels = 2,
            OutputAudioCodec = "ac3",
            OutputContainer = "ac3"
        };

        return (state, mediaSource, externalAudio);
    }
}
