using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz.Logging;
using VibeCore.Areas.Api.Controllers;
using VibeCore.Data;
using VibeCore.Scheduling;
using Xunit;

namespace VibeCoreWeb.Tests;

public sealed class FlexSsoIntegrationTests : IDisposable
{
    private const string Authority = "https://flex.test";
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"vibecore-tests-{Guid.NewGuid():N}.db");
    private readonly VibeCoreFactory _factory;

    public FlexSsoIntegrationTests()
    {
        _factory = new VibeCoreFactory(_databasePath);
    }

    [Fact]
    public async Task Protected_pages_challenge_and_apis_return_unauthorized()
    {
        using var client = CreateClient();

        using var pageResponse = await client.GetAsync("/app/");
        using var apiResponse = await client.GetAsync("/api/user/current");

        Assert.Equal(HttpStatusCode.Redirect, pageResponse.StatusCode);
        Assert.StartsWith(
            "/flex-auth/login?ReturnUrl=",
            pageResponse.Headers.Location?.PathAndQuery,
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, apiResponse.StatusCode);
    }

    [Theory]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/Identity/Account/Manage")]
    public async Task Removed_identity_routes_return_not_found(string path)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sso_callback_exposes_claim_identity_and_reader_access()
    {
        using var client = CreateClient();
        using var callbackResponse = await SignInAsync(client);
        using var userResponse = await client.GetAsync("/api/user/current");
        using var handlersResponse = await client.GetAsync("/api/scheduled-tasks/handlers");
        var user = await userResponse.Content.ReadFromJsonAsync<UserInfoDto>();

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.Equal("/app/", callbackResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        Assert.NotNull(user);
        Assert.True(user.IsAuthenticated);
        Assert.Equal("019f85f6-6c57-72c9-9fdc-afd06984a7c2", user.UserId);
        Assert.Equal("Ada Lovelace", user.UserName);
        Assert.Equal("ada@example.test", user.Email);
        Assert.Equal("tenant-123", user.TenantId);
        Assert.Equal("Product", user.TenantRole);
        Assert.Contains("Reader", user.Roles);
        Assert.Equal(HttpStatusCode.OK, handlersResponse.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_open_domain_free_app()
    {
        using var client = CreateClient();
        using var callbackResponse = await SignInAsync(client);
        using var appResponse = await client.GetAsync("/app/");
        var html = await appResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = Regex.Match(
            html,
            "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(antiforgeryToken);
        Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
    }

    [Fact]
    public async Task Reader_can_inspect_scheduled_tasks_but_cannot_mutate_them()
    {
        using var client = CreateClient();
        using var callbackResponse = await SignInAsync(client);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        using var handlersResponse = await client.GetAsync("/api/scheduled-tasks/handlers");
        var handlers = await handlersResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, handlersResponse.StatusCode);
        Assert.Contains(handlers.EnumerateArray(), handler =>
            handler.GetProperty("key").GetString() == "integration-task");

        using var createRequest = CreateScheduleRequest(antiforgeryToken);
        using var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Operator_can_manage_and_trigger_a_scheduled_task()
    {
        _factory.Roles = ["Reader", "Operator"];
        using var client = CreateClient();
        using var callbackResponse = await SignInAsync(client);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        using var createRequest = CreateScheduleRequest(antiforgeryToken);
        using var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var id = created.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));

        foreach (var action in new[] { "pause", "resume", "run" })
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/scheduled-tasks/schedules/{id}/{action}");
            request.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(
                action == "run" ? HttpStatusCode.Accepted : HttpStatusCode.NoContent,
                response.StatusCode);
        }

        JsonElement runs = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var runsResponse = await client.GetAsync(
                $"/api/scheduled-tasks/schedules/{id}/runs");
            var content = await runsResponse.Content.ReadAsStringAsync();
            Assert.True(
                runsResponse.IsSuccessStatusCode,
                $"Run history returned {(int)runsResponse.StatusCode}: {content}");
            runs = JsonSerializer.Deserialize<JsonElement>(content);
            if (runs.EnumerateArray().Any(run =>
                    run.GetProperty("status").GetString() == "Succeeded"))
                break;
            await Task.Delay(50);
        }
        Assert.Contains(runs.EnumerateArray(), run =>
            run.GetProperty("status").GetString() == "Succeeded");

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/scheduled-tasks/schedules/{id}");
        deleteRequest.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public void Ef_model_contains_no_identity_tables()
    {
        var context = new ApplicationDbContextFactory().CreateDbContext([]);
        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null)
            .ToArray();

        Assert.DoesNotContain("Todos", tables);
        Assert.Contains("ScheduledTaskRuns", tables);
        Assert.Contains("DataProtectionKeys", tables);
        Assert.DoesNotContain(tables, table => table!.StartsWith("AspNet", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private HttpClient CreateClient()
    {
        // Quartz's Microsoft logging bridge is process-global. A previous
        // WebApplicationFactory may have disposed the factory it installed.
        LogProvider.SetCurrentLogProvider(null!);
        LogProvider.IsDisabled = true;
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://preview.test"),
            HandleCookies = true,
        });
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client)
    {
        using var loginResponse = await client.GetAsync(
            "/flex-auth/login?returnUrl=/app/");
        var authorizeUrl = Assert.IsType<Uri>(loginResponse.Headers.Location);
        var state = QueryHelpers.ParseQuery(authorizeUrl.Query)["state"].ToString();

        return await client.GetAsync(
            $"/flex-auth/callback?code=test-code&state={Uri.EscapeDataString(state)}" +
            $"&iss={Uri.EscapeDataString(Authority)}");
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var appResponse = await client.GetAsync("/app/");
        var html = await appResponse.Content.ReadAsStringAsync();
        var token = Regex.Match(
            html,
            "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
        return Assert.IsType<string>(token is { Length: > 0 } ? token : null);
    }

    private static HttpRequestMessage CreateScheduleRequest(string antiforgeryToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/scheduled-tasks/schedules")
        {
            Content = JsonContent.Create(new
            {
                name = "Integration schedule",
                description = "Runs the harmless example handler.",
                handlerKey = "integration-task",
                kind = "OneTime",
                runAt = DateTimeOffset.UtcNow.AddHours(1),
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        return request;
    }

    private sealed class VibeCoreFactory(string databasePath)
        : WebApplicationFactory<Program>
    {
        public string[] Roles { get; set; } = ["Reader"];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}",
                    ["Database:Provider"] = "Sqlite",
                    ["DataProtection:PersistKeysToDatabase"] = "false",
                    ["FlexSso:Authority"] = Authority,
                    ["FlexSso:BackchannelAuthority"] = Authority,
                }));
            builder.ConfigureServices(services =>
            {
                services.AddScheduledTask<TestScheduledTask>(
                    "integration-task",
                    "Integration task",
                    "A harmless handler registered only by integration tests.");
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(
                    new StubHttpClientFactory(() => Roles));
            });
        }
    }

    private sealed class TestScheduledTask : IScheduledTaskHandler
    {
        public Task ExecuteAsync(
            ScheduledTaskExecutionContext context,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubHttpClientFactory(Func<string[]> getRoles) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubTokenHandler(getRoles));
    }

    private sealed class StubTokenHandler(Func<string[]> getRoles) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new
            {
                userId = "019f85f6-6c57-72c9-9fdc-afd06984a7c2",
                email = "ada@example.test",
                name = "Ada Lovelace",
                tenantId = "tenant-123",
                tenantRole = "Product",
                roles = getRoles(),
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            });
        }
    }
}
