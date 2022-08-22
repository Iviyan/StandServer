global using System.Text;
global using System.Data;
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
global using VueCliMiddleware;
global using Microsoft.AspNetCore.Localization;
global using Npgsql;
global using Dapper;
global using CsvHelper;
global using FluentValidation;
global using FluentValidation.AspNetCore;
global using EFCore.BulkExtensions;
global using StandServer.Database;
global using StandServer.Models;
global using StandServer.Utils;
global using StandServer.Hubs;
using StandServer.Configuration;
using StandServer;
using StandServer.Services;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

// Database configuration

string connection = builder.Configuration.GetConnectionString("PgsqlConnection");
services.AddDbContext<ApplicationContext>(options => options.UseNpgsql(connection));
services.AddSingleton<DatabaseSource>();
services.AddScoped<DatabaseContext>();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// SignalR configuration

services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(SnakeCaseNamingPolicy.Instance));
        options.PayloadSerializerOptions.Converters.Add(new StandServer.Utils.DateTimeConverter());
    });

// FluentValidation configuration (part)

ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("en");
ValidatorOptions.Global.DisplayNameResolver =
    (type, member, expression) => SnakeCaseNamingPolicy.ToSnakeCase(member.Name);

// Controllers, JSON, FluentValidation configuration

services.AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new DateTimeModelBinderProvider());
        options.InputFormatters.Insert(0, new RawRequestBodyFormatter());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(SnakeCaseNamingPolicy.Instance));
#if !DEBUG
        options.AllowInputFormatterExceptionMessages = false;
#endif
        options.JsonSerializerOptions.Converters.Add(new StandServer.Utils.DateTimeConverter());
    })
    .AddFluentValidation(fv =>
    {
        fv.DisableDataAnnotationsValidation = true;
        fv.RegisterValidatorsFromAssemblyContaining<Program>(lifetime: ServiceLifetime.Singleton);
    });

// JWT configuration

services.Configure<JwtConfig>(configuration.GetSection(JwtConfig.SectionName));
services.Configure<StandInfo>(configuration.GetSection(StandInfo.SectionName));

var jwtConfig = configuration.GetSection(JwtConfig.SectionName).Get<JwtConfig>();

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

services.AddSingleton<CachedData>(); // Some frequently used data
services.AddTransient<LoadCacheService>(); // A service that load data once when the application starts

services.AddScoped<RequestData>(); // Information about the user who made the request

// SPA configuration
services.AddSpaStaticFiles(options => options.RootPath = "ClientApp/dist");

// A service that removes obsolete refresh tokens
services.AddHostedService<ClearOldRefreshTokensService>(); 

// A service that records the moment when receiving data from the stand stops 
services.AddHostedService<ConnectionLossDetectionService>(); 

//services.AddHostedService<HubTestService>(); // SignalR Health Testing Service

var app = builder.Build();

// Loading cache data
var loadCacheService = app.Services.GetRequiredService<LoadCacheService>();
await loadCacheService.StartAsync(default);
await loadCacheService.ExecuteTask;

if (app.Environment.IsDevelopment()) { }

app.UseSpaStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Get device uid, user id, user login for every request. Sets the device uid if it doesn't exist.
app.Use(async (context, next) =>
{
    RequestData requestData = context.RequestServices.GetRequiredService<RequestData>();
    if (!context.Request.Cookies.TryGetValue("DeviceUid", out string? deviceSUid)
        || !Guid.TryParse(deviceSUid, out Guid deviceUid))
    {
        requestData.DeviceUid = Guid.NewGuid();
        context.Response.Cookies.Append("DeviceUid", requestData.DeviceUid.ToString(),
            new CookieOptions { HttpOnly = true, Expires = DateTimeOffset.FromUnixTimeSeconds(int.MaxValue) });
    }
    else
        requestData.DeviceUid = deviceUid;

    if (context.User.Identity?.IsAuthenticated is true)
    {
        requestData.UserId = int.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        requestData.UserLogin = context.User.FindFirst(JwtRegisteredClaimNames.Name)!.Value;
    }

    await next();
});

app.MapHub<StandHub>("/stand-hub");

app.MapControllers();

/*app.MapToVueCliProxy(
    "{*path}",
    new SpaOptions { SourcePath = "ClientApp" },
    npmScript: !System.Diagnostics.Debugger.IsAttached ? "serve" : null,
    //npmScript: "serve",
    regex: "Compiled successfully",
    forceKill: true
);*/

// https://github.com/dotnet/aspnetcore/issues/5223#issuecomment-433394061

var spaApp = ((IEndpointRouteBuilder)app).CreateApplicationBuilder();
spaApp.Use(next => context =>
{
    // Set endpoint to null so the SPA middleware will handle the request.
    context.SetEndpoint(null);
    return next(context);
});

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

app.MapFallback("{*path}", spaApp.Build()); // default is {*path:nonfile}

app.Run();