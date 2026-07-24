using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using Quartz;
using VibeCore.Auth;
using VibeCore.Data;
using VibeCore.Scheduling;
using VibeCore.Security;
using Vite.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var isPreviewMode =
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PREVIEW_URL"));
var isBuildTimeOpenApiGeneration = string.Equals(
    Environment.GetEnvironmentVariable("VIBECORE_GENERATE_OPENAPI"),
    "true",
    StringComparison.OrdinalIgnoreCase);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is required. Configure it with " +
        "ConnectionStrings__DefaultConnection in deployed environments.");
}

var databaseProvider = builder.Configuration["Database:Provider"] ?? "PostgreSql";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
        return;
    }

    if (databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
        return;
    }

    throw new InvalidOperationException(
        $"Unsupported database provider '{databaseProvider}'. Use 'PostgreSql' or 'Sqlite'.");
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddScheduledTask<TodoSummaryTask>(
        "todo-summary",
        "Todo summary",
        "Counts incomplete todos and writes the result to the server log.")
    .AddScoped<ScheduledTaskService>()
    .AddScoped<ScheduledTaskExecutor>();
if (!isBuildTimeOpenApiGeneration)
{
    builder.Services.AddHostedService<QuartzSqliteSchemaInitializer>();
}
builder.Services.AddQuartz(quartz =>
{
    quartz.SchedulerName = "VibeCore";
    quartz.UseDefaultThreadPool(pool => pool.MaxConcurrency = 4);
    quartz.UsePersistentStore(store =>
    {
        store.UseProperties = true;
        store.UseSystemTextJsonSerializer();
        if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            store.UseMicrosoftSQLite(connectionString);
        }
        else
        {
            store.UseGenericDatabase(
                "Npgsql",
                provider => provider.ConnectionString = connectionString);
            store.UseClustering();
        }
    });
});
if (!isBuildTimeOpenApiGeneration)
{
    builder.Services.AddQuartzHostedService(options =>
    {
        options.WaitForJobsToComplete = true;
    });
}
builder.Services.Configure<HostOptions>(options =>
    options.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = FlexSsoDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = FlexSsoDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = FlexSsoDefaults.AuthenticationScheme;
    })
    .AddCookie(FlexSsoDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "__Host-VibeCore.FlexSso";
        options.Cookie.HttpOnly = true;
        // Preview apps are embedded on the Flex host, so their authentication
        // cookie must be available from a cross-site iframe.
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/flex-auth/login";
        options.AccessDeniedPath = "/flex-auth/denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.Reader, policy =>
        policy.RequireRole("Reader", "Editor", "Operator", "Administrator"))
    .AddPolicy(AppPolicies.Editor, policy =>
        policy.RequireRole("Editor", "Administrator"))
    .AddPolicy(AppPolicies.Operator, policy =>
        policy.RequireRole("Operator", "Administrator"))
    .AddPolicy(AppPolicies.Administrator, policy =>
        policy.RequireRole("Administrator"));

builder.Services.AddRazorPages(options =>
    options.Conventions.AuthorizeFolder("/App"));
builder.Services.AddControllers(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-VibeCore.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("VibeCore");
if (builder.Configuration.GetValue("DataProtection:PersistKeysToDatabase", true))
{
    dataProtection.PersistKeysToDbContext<ApplicationDbContext>();
}

var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>(
        "database",
        tags: ["ready"]);
healthChecks.AddCheck<SchedulerReadinessHealthCheck>(
    "scheduler",
    tags: ["ready"]);
if (builder.Environment.IsDevelopment() || isPreviewMode)
{
    healthChecks.AddCheck(
        "vite",
        new ViteReadinessHealthCheck(viteServerPort: null),
        tags: ["ready"]);
}
builder.Services
    .AddOptions<FlexSsoOptions>()
    .Bind(builder.Configuration.GetSection(FlexSsoOptions.SectionName))
    .Validate(
        options => FlexSsoOptions.IsValidHttpAuthority(options.Authority),
        "FlexSso:Authority must be an absolute HTTP(S) URL.")
    .Validate(
        options => string.IsNullOrWhiteSpace(options.BackchannelAuthority) ||
            FlexSsoOptions.IsValidHttpAuthority(options.BackchannelAuthority),
        "FlexSso:BackchannelAuthority must be an absolute HTTP(S) URL.")
    .ValidateOnStart();
builder.Services.AddHttpClient(FlexSsoDefaults.HttpClientName);
builder.Services.AddSingleton<FlexSsoTransactionProtector>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VibeCore API", Version = "v1" });
});

var viteServerPort = int.TryParse(
    Environment.GetEnvironmentVariable("VITE_PORT"),
    out var parsedVitePort)
    ? parsedVitePort
    : 5173;
var viteServerHost = Environment.GetEnvironmentVariable("VITE_SERVER_HOST");
viteServerHost ??= "localhost";

builder.Services.AddViteServices(options =>
{
    options.Base = "/app/";
    options.Manifest = ".vite/manifest.json";
    options.Server.AutoRun = false;
    options.Server.Host = viteServerHost;
    options.Server.Port = (ushort)viteServerPort;
    options.Server.UseReactRefresh = true;
    options.Server.PackageDirectory = "ClientApp";
    options.Server.PackageManager = "npm";
});

var app = builder.Build();

if (!isBuildTimeOpenApiGeneration &&
    app.Environment.IsDevelopment() &&
    databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeCore API v1");
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseWebSockets();

// Keep Vite on an internal port and proxy its assets/HMR through ASP.NET's
// single public port, matching the proven FlexEnv sqlite preview setup.
if (app.Environment.IsDevelopment() || isPreviewMode)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";

        if (context.WebSockets.IsWebSocketRequest && path == "/app/__vite_hmr")
        {
            try
            {
                using var viteSocket = new System.Net.WebSockets.ClientWebSocket();
                viteSocket.Options.AddSubProtocol("vite-hmr");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await viteSocket.ConnectAsync(
                    new Uri($"ws://localhost:{viteServerPort}{path}"),
                    timeout.Token);

                using var browserSocket =
                    await context.WebSockets.AcceptWebSocketAsync("vite-hmr");
                await Task.WhenAny(
                    RelayWebSocket(viteSocket, browserSocket),
                    RelayWebSocket(browserSocket, viteSocket));

                if (viteSocket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    await viteSocket.CloseAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Done",
                        CancellationToken.None);
                }
                if (browserSocket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    await browserSocket.CloseAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Done",
                        CancellationToken.None);
                }
                return;
            }
            catch (Exception ex) when (
                ex is OperationCanceledException or
                System.Net.WebSockets.WebSocketException or
                HttpRequestException)
            {
                app.Logger.LogWarning(ex, "Could not proxy the Vite HMR connection");
            }
        }

        var shouldProxy = path.StartsWith("/app/", StringComparison.Ordinal) &&
            (Path.HasExtension(path) ||
             path.StartsWith("/app/@", StringComparison.Ordinal) ||
             path.StartsWith("/app/src/", StringComparison.Ordinal) ||
             path.StartsWith("/app/node_modules/", StringComparison.Ordinal));

        if (shouldProxy)
        {
            using var httpClient = new HttpClient();
            var viteUrl =
                $"http://localhost:{viteServerPort}{path}{context.Request.QueryString}";
            try
            {
                using var viteRequest = new HttpRequestMessage(HttpMethod.Get, viteUrl);
                if (context.Request.Headers.TryGetValue("Accept", out var accept))
                {
                    viteRequest.Headers.TryAddWithoutValidation(
                        "Accept",
                        accept.ToArray());
                }

                using var response = await httpClient.SendAsync(
                    viteRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                if (response.Content.Headers.ContentType is not null)
                {
                    context.Response.ContentType =
                        response.Content.Headers.ContentType.ToString();
                }
                context.Response.Headers.CacheControl =
                    "no-store, no-cache, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
                await response.Content.CopyToAsync(
                    context.Response.Body,
                    context.RequestAborted);
                return;
            }
            catch (HttpRequestException ex)
            {
                app.Logger.LogWarning(ex, "Could not proxy Vite asset {Path}", path);
            }
        }

        await next();
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks(
        "/health/live",
        new HealthCheckOptions
        {
            Predicate = _ => false
        })
    .AllowAnonymous();
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = registration =>
                registration.Tags.Contains("ready")
        })
    .AllowAnonymous();
if (!isBuildTimeOpenApiGeneration)
{
    app.MapStaticAssets();
}
app.MapRazorPages()
    .WithStaticAssets();

if (!isBuildTimeOpenApiGeneration &&
    (app.Environment.IsDevelopment() || isPreviewMode))
{
    app.UseViteDevelopmentServer(true);
}

app.Run();

static async Task RelayWebSocket(
    System.Net.WebSockets.WebSocket source,
    System.Net.WebSockets.WebSocket destination)
{
    var buffer = new byte[4096];
    try
    {
        while (source.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await source.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                break;
            if (destination.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    CancellationToken.None);
            }
        }
    }
    catch (System.Net.WebSockets.WebSocketException)
    {
        // Either side closed the development-only HMR connection.
    }
}

sealed class DatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>()
                .Database;
            return await database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("The application database is reachable.")
                : HealthCheckResult.Unhealthy("The application database is not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "The application database readiness check failed.",
                ex);
        }
    }
}

sealed class SchedulerReadinessHealthCheck(ISchedulerFactory schedulerFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            return scheduler.IsStarted && !scheduler.IsShutdown
                ? HealthCheckResult.Healthy("The scheduled task engine is running.")
                : HealthCheckResult.Unhealthy("The scheduled task engine is not running.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The scheduled task readiness check failed.", ex);
        }
    }
}

sealed class ViteReadinessHealthCheck : IHealthCheck
{
    private readonly int? _viteServerPort;

    public ViteReadinessHealthCheck(int? viteServerPort)
    {
        _viteServerPort = viteServerPort;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var port = _viteServerPort ??
            (int.TryParse(
                Environment.GetEnvironmentVariable("VITE_PORT"),
                out var configuredPort)
                ? configuredPort
                : 5173);
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            using var response = await client.GetAsync(
                $"http://127.0.0.1:{port}/app/@vite/client",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("The Vite development server is reachable.")
                : HealthCheckResult.Unhealthy(
                    $"The Vite development server returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "The Vite development server is not reachable.",
                ex);
        }
    }
}

public partial class Program
{
}
