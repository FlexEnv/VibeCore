using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using VibeCore.Auth;
using VibeCore.Data;
using VibeCore.Security;
using Vite.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var flexSsoEnabled = builder.Configuration.GetValue<bool>("FlexSso:Enabled");

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

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

if (flexSsoEnabled)
{
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
            options.Cookie.SameSite = SameSiteMode.Lax;
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
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.Reader, policy =>
        policy.RequireRole("Reader", "Editor", "Operator", "Administrator"))
    .AddPolicy(AppPolicies.Editor, policy =>
        policy.RequireRole("Editor", "Administrator"))
    .AddPolicy(AppPolicies.Operator, policy =>
        policy.RequireRole("Operator", "Administrator"))
    .AddPolicy(AppPolicies.Administrator, policy =>
        policy.RequireRole("Administrator"));

var razorPages = builder.Services.AddRazorPages();
if (flexSsoEnabled)
{
    razorPages.AddRazorPagesOptions(options =>
        options.Conventions.AuthorizeFolder("/App"));
}
builder.Services.AddControllers(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

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

builder.Services.AddHealthChecks();
builder.Services.Configure<FlexSsoOptions>(builder.Configuration.GetSection(FlexSsoOptions.SectionName));
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

builder.Services.AddViteServices(options =>
{
    options.Base = "/app/";
    options.Manifest = ".vite/manifest.json";
    options.Server.AutoRun = false;
    options.Server.Port = 5173;
    options.Server.UseReactRefresh = true;
    options.Server.PackageDirectory = "ClientApp";
    options.Server.PackageManager = "npm";
});

var app = builder.Build();

if (app.Environment.IsDevelopment() &&
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
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.UseWebSockets();
    app.UseViteDevelopmentServer(true);
}

app.Run();
