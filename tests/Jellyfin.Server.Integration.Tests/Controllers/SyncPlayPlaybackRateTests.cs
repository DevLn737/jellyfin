using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Api.Models.SyncPlayDtos;
using Jellyfin.Extensions.Json;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.SyncPlay;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class SyncPlayPlaybackRateTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public SyncPlayPlaybackRateTests(JellyfinApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PlaybackRate_ApiValidationAndCapabilityRoutes_KeepLegacyClientsSafe()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var accessToken = await AuthHelper.CompleteStartupAsync(client);
        client.DefaultRequestHeaders.AddAuthHeader(accessToken);

        using var createResponse = await client.PostAsJsonAsync(
            "/SyncPlay/New",
            new NewGroupRequestDto { GroupName = "PlaybackRateContract" },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var group = await createResponse.Content.ReadFromJsonAsync<GroupInfoDto>(JsonDefaults.Options, cancellationToken);
        Assert.NotNull(group);

        using var legacyResponse = await client.PostAsJsonAsync(
            "/SyncPlay/SetPlaybackRate",
            new SetPlaybackRateRequestDto { PlaybackRate = 1.5F },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, legacyResponse.StatusCode);
        using var problem = JsonDocument.Parse(await legacyResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("SyncPlayPlaybackRateNotSupported", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, problem.RootElement.GetProperty("unsupportedParticipants").GetInt32());

        using var queryCapabilities = await client.PostAsync(
            "/Sessions/Capabilities?playableMediaTypes=Video&supportedCommands=Play&supportsSyncPlayPlaybackRate=true",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, queryCapabilities.StatusCode);
        using var supportedResponse = await client.PostAsJsonAsync(
            "/SyncPlay/SetPlaybackRate",
            new SetPlaybackRateRequestDto { PlaybackRate = 1.25F },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, supportedResponse.StatusCode);

        foreach (var invalidJson in new[] { "{}", "{\"PlaybackRate\":0.24}", "{\"PlaybackRate\":5.01}", "{\"PlaybackRate\":\"NaN\"}", "{\"PlaybackRate\":\"Infinity\"}" })
        {
            using var invalidContent = new StringContent(invalidJson, Encoding.UTF8, "application/json");
            using var invalidResponse = await client.PostAsync("/SyncPlay/SetPlaybackRate", invalidContent, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }

        // Omitting the capability from the full route must reset it to the legacy default.
        using var legacyCapabilities = await client.PostAsJsonAsync(
            "/Sessions/Capabilities/Full",
            new ClientCapabilitiesDto(),
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, legacyCapabilities.StatusCode);
        using var acceleratedJoin = await client.PostAsJsonAsync(
            "/SyncPlay/Join",
            new JoinGroupRequestDto { GroupId = group.GroupId },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, acceleratedJoin.StatusCode);

        using var resetResponse = await client.PostAsJsonAsync(
            "/SyncPlay/SetPlaybackRate",
            new SetPlaybackRateRequestDto { PlaybackRate = 1.0F },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        using var fullCapabilities = await client.PostAsJsonAsync(
            "/Sessions/Capabilities/Full",
            new ClientCapabilitiesDto { SupportsSyncPlayPlaybackRate = true },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, fullCapabilities.StatusCode);
        using var fullSupportedResponse = await client.PostAsJsonAsync(
            "/SyncPlay/SetPlaybackRate",
            new SetPlaybackRateRequestDto { PlaybackRate = 1.5F },
            JsonDefaults.Options,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, fullSupportedResponse.StatusCode);
    }
}
