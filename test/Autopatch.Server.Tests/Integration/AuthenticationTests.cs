using System.Security.Claims;
using System.Text.Encodings.Web;
using Autopatch.Client.Models;
using Autopatch.Server.Extensions;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Tests.Integration;

[TestClass]
public sealed class AuthenticationTests
{
    private const string Token = "valid-test-token";

    [TestMethod]
    [TestCategory("C4")]
    public async Task C4_AccessToken_ReachesTheHubAsAuthenticatedUser()
    {
        await using var server = await AutoPatchTestServer.StartAsync(
            configureServices: services =>
            {
                services.AddAuthentication(BearerTokenHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, BearerTokenHandler>(BearerTokenHandler.SchemeName, _ => { });
                services.AddAuthorization();
                services.AddTrackedCollection<SecureItem, AuthenticatedUserValidator<SecureItem>>();
            },
            configureApp: app =>
            {
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseAutoPatch();
            });

        var tokenProvider = typeof(AutoPatchConfiguration).GetProperty("AccessTokenProvider");
        tokenProvider.Should().NotBeNull("AutoPatchConfiguration has no AccessTokenProvider and WithUrl is called without options, so no bearer token can be sent");

        await using var client = await TestClient.ConnectAsync(server.BaseUrl, config =>
            tokenProvider!.SetValue(config, (Func<Task<string?>>)(() => Task.FromResult<string?>(Token))));
        var accepted = await client.Client.SubscribeToTypeAsync<SecureItem>();

        accepted.Should().BeTrue("the validator only accepts connections whose Context.User is authenticated");
    }

    private sealed class BearerTokenHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.Ordinal)
                ? header["Bearer ".Length..]
                : Request.Query["access_token"].ToString();

            if (token != Token)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
