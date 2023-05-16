global using System.Text;
global using System.Net.Mime;
global using System.Text.Json;
global using System.Security.Claims;
global using System.IdentityModel.Tokens.Jwt;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.IdentityModel.Tokens;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.Extensions.Options;
global using Microsoft.AspNetCore.SpaServices;
global using Microsoft.AspNetCore.SignalR;
global using Microsoft.EntityFrameworkCore;
global using System.Text.Json.Serialization;
global using System.ComponentModel;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using System.Globalization;
global using Microsoft.Extensions.Localization;
global using VueCliMiddleware;
global using Microsoft.AspNetCore.Localization;
global using Npgsql;
global using Dapper;
global using CsvHelper;
global using FluentValidation;
global using FluentValidation.AspNetCore;
global using StandServer.Database;
global using StandServer.Models;
global using StandServer.Utils;
global using StandServer.Hubs;
using StandServer.Configuration;
using StandServer;
using StandServer.Controllers;
using StandServer.Services;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

configuration
    .AddJsonFile("secrets.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"secrets.{builder.Environment.EnvironmentName}.json",
        optional: true, reloadOnChange: true);

// Database configuration

string connectionString = builder.Configuration.GetConnectionString("PgsqlConnection")!;
services.AddNpgsqlDataSource(connectionString, ApplicationContext.ConfigureNpgsqlBuilder);

services.AddDbContext<ApplicationContext>(options =>
{
    options.UseNpgsql();
});
services.AddScoped<DatabaseContext>();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// SignalR configuration

services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = CamelCaseNamingPolicy.Instance;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(CamelCaseNamingPolicy.Instance));
        options.PayloadSerializerOptions.Converters.Add(new DateTimeJsonConverter());
    });

// Controllers, JSON configuration

services.AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new DateTimeModelBinderProvider());
        options.ValueProviderFactories.Add(new SnakeCaseQueryValueProviderFactory());
        options.InputFormatters.Insert(0, new RawRequestBodyFormatter());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = CamelCaseNamingPolicy.Instance;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(CamelCaseNamingPolicy.Instance));
#if !DEBUG
        options.AllowInputFormatterExceptionMessages = false;
#endif
        options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
    });

// Localization

services.AddLocalization(options => options.ResourcesPath = "Resources");

// Localize default HttpValidationProblemDetails error

CultureInfo enUsCulture = new("en-US");
services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (context.ProblemDetails.Title == "One or more validation errors occurred."
        && !CultureInfo.CurrentCulture.Equals(enUsCulture))
    {
        var localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Program>>();
        var localizedString = localizer.GetString("One or more validation errors occurred");
        if (localizedString.ResourceNotFound == false) context.ProblemDetails.Title = localizedString.Value;
    }
});

// FluentValidation configuration

ValidatorOptions.Global.DisplayNameResolver = (type, member, expression) => CamelCaseNamingPolicy.FromPascalToCamelCase(member.Name);
ValidatorOptions.Global.PropertyNameResolver = (type, member, expression) => CamelCaseNamingPolicy.FromPascalToCamelCase(member.Name);
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

services.AddFluentValidationAutoValidation(o =>
{
    o.DisableDataAnnotationsValidation = true;
});
services.AddValidatorsFromAssemblyContaining<Program>(lifetime: ServiceLifetime.Singleton);

// Auth configuration

services.Configure<AuthConfig>(configuration.GetSection(AuthConfig.SectionName));

var authConfig = configuration.GetSection(AuthConfig.SectionName).Get<AuthConfig>()!;
var jwtConfig = authConfig.Jwt;

var tokenValidationParams = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.Secret)),
    ValidIssuer = jwtConfig.Issuer,
    ValidateIssuer = true,
    ValidateAudience = false,
    ValidateLifetime = true,
    RequireExpirationTime = false,
    ClockSkew = TimeSpan.Zero,
};

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Authentication with JWT configuration

services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(jwt =>
    {
        jwt.SaveToken = true;
        jwt.TokenValidationParameters = tokenValidationParams;
    });

services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicy.Admin, policy => policy.RequireClaim("IsAdmin", true.ToString()));
});

services.AddSingleton<CachedData>(); // Some frequently used data
services.AddTransient<LoadCacheService>(); // A service that load data once when the application starts

// Configuration based on DB table
services.AddSingleton<ApplicationConfiguration>();
services.AddSingleton<IApplicationConfiguration, ApplicationConfiguration>(
    sp => sp.GetRequiredService<ApplicationConfiguration>());
services.AddScoped<DbStoredConfigurationService>();

services.AddScoped<RequestData>(); // Information about the user who made the request

// SPA configuration
services.AddSpaStaticFiles(options => options.RootPath = "ClientApp/dist");

// A service that removes obsolete refresh tokens
services.AddHostedService<ClearOldRefreshTokensService>();

// Notifications configuration
services.Configure<NotificationsConfig>(configuration.GetSection(NotificationsConfig.SectionName));

// Telegram notification service
services.AddTelegramService();

var app = builder.Build();

Console.WriteLine("Current culture: " + CultureInfo.CurrentCulture);


using (var scope = app.Services.CreateScope())
{
    // Creating a user if there are no users in the system.
    var efContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    if (!await efContext.Users.AnyAsync())
    {
        var firstUser = authConfig.FirstUser;

        User user = new()
        {
            Login = firstUser.Login,
            Password = AuthController.GetHashPassword(firstUser.Password),
            IsAdmin = true
        };
        efContext.Users.Add(user);
        await efContext.SaveChangesAsync();
    }
    
    // Loading cache data
    var loadCacheService = scope.ServiceProvider.GetRequiredService<LoadCacheService>(); 
    await loadCacheService.LoadAsync();
     
    // Load app configuration
    var dbStoredConfiguration = scope.ServiceProvider.GetRequiredService<DbStoredConfigurationService>(); 
    await dbStoredConfiguration.LoadAllAsync();
}

// --- Request processing ---

var spaApp = ((IEndpointRouteBuilder)app).CreateApplicationBuilder();

spaApp.UseFixedSpa(spaBuilder =>
{
    spaBuilder.Options.SourcePath = "ClientApp";

    if (System.Diagnostics.Debugger.IsAttached)
    {
        spaBuilder.UseVueCli(
            npmScript: "serve",
            port: 8080,
            https: false,
            runner: ScriptRunnerType.Npm,
            regex: "Compiled successfully",
            forceKill: true,
            wsl: false);
    }
});

app.UseSpaStaticFiles(new() { ServeUnknownFileTypes = true });

app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api") && context.Request.Method == "GET",
    applicationBuilder => applicationBuilder.Run(spaApp.Build()));

app.UsePathBase("/api");

var supportedCultures = new[]
{
    new CultureInfo("en-US"),
    new CultureInfo("ru-RU"),
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Get device uid, user id, user login for every request. Sets the device uid if it doesn't exist.
app.Use(async (context, next) =>
{
    RequestData requestData = context.RequestServices.GetRequiredService<RequestData>();

    if (context.User.Identity?.IsAuthenticated is true)
    {
        if (context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { } userId)
            requestData.UserId = int.Parse(userId);
        if (context.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value is { } login)
            requestData.UserLogin = login;
        if (context.User.FindFirst("IsAdmin")?.Value is { } isAdmin)
            requestData.IsAdmin = Boolean.TryParse(isAdmin, out bool val) && val;
        if (context.User.FindFirst("DeviceUid")?.Value is { } deviceUidRaw
            && Guid.TryParseExact(deviceUidRaw, "N", out Guid deviceUid))
            requestData.DeviceUid = deviceUid;
    }

    await next();
});

app.MapHub<StandHub>("/stand-hub"); // /api/stand-hub

app.MapControllers();

app.Run();