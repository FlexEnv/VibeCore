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
using VibeCore.Areas.Api.Controllers;
using VibeCore.Data;
using VibeCore.Models;
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
        using var apiResponse = await client.GetAsync("/api/todos");

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
        using var todosResponse = await client.GetAsync("/api/todos");
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
        Assert.Equal(HttpStatusCode.OK, todosResponse.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_complete_todo_crud()
    {
        using var client = CreateClient();
        using var callbackResponse = await SignInAsync(client);
        using var appResponse = await client.GetAsync("/app/");
        var html = await appResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = Regex.Match(
            html,
            "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(antiforgeryToken);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/todos")
        {
            Content = JsonContent.Create(new TodoItem { Title = "Test SSO todo" }),
        };
        createRequest.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        using var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        using var completeRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/todos/{created.Id}/complete");
        completeRequest.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        using var completeResponse = await client.SendAsync(completeRequest);
        Assert.Equal(HttpStatusCode.NoContent, completeResponse.StatusCode);

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/todos/{created.Id}");
        deleteRequest.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var listResponse = await client.GetAsync("/api/todos");
        var todos = await listResponse.Content.ReadFromJsonAsync<TodoItem[]>();
        Assert.Empty(Assert.IsType<TodoItem[]>(todos));
    }

    [Fact]
    public void Ef_model_contains_no_identity_tables()
    {
        var context = new ApplicationDbContextFactory().CreateDbContext([]);
        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null)
            .ToArray();

        Assert.Contains("Todos", tables);
        Assert.Contains("DataProtectionKeys", tables);
        Assert.DoesNotContain(tables, table => table!.StartsWith("AspNet", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private HttpClient CreateClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://preview.test"),
            HandleCookies = true,
        });

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

    private sealed class VibeCoreFactory(string databasePath)
        : WebApplicationFactory<Program>
    {
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
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory());
            });
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubTokenHandler());
    }

    private sealed class StubTokenHandler : HttpMessageHandler
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
                roles = new[] { "Reader" },
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            });
        }
    }
}
