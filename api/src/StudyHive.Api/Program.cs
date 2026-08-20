using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Middleware;
using StudyHive.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// ---- JSON: camelCase over the wire, enums as strings (DOCS shared conventions sec. 12) ----
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "StudyHive API", Version = "v1" });
    var bearerScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste a JWT access token."
    };
    c.AddSecurityDefinition("Bearer", bearerScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { bearerScheme, Array.Empty<string>() }
    });
});

// ---- PostgreSQL, snake_case column/table naming (DOCS shared conventions sec. 12) ----
// Outside Development a missing connection string fails startup instead of silently pointing at
// a well-known local dev password (Codex security review, P1).
var connectionString = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5432;Database=studyhive;Username=studyhive;Password=studyhive_dev"
    : builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured outside Development.");

builder.Services.AddDbContext<StudyHiveDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// ---- Options ----
builder.Services.Configure<WorkflowLimitsOptions>(builder.Configuration.GetSection(WorkflowLimitsOptions.SectionName));
builder.Services.Configure<DevSeedOptions>(builder.Configuration.GetSection(DevSeedOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtIssuer = jwtSection["Issuer"] ?? "studyhive-dev";
var jwtAudience = jwtSection["Audience"] ?? "studyhive-dev-clients";

// A known signing key would let anyone forge a valid access token, so — unlike issuer/audience,
// which only affect validation matching — this has no Development-vs-not fallback distinction:
// it must always come from configuration, and must always be long enough for HS256 (Codex
// security review, P1).
var jwtSigningKey = jwtSection["SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 bytes.");
}

// ---- JWT bearer auth + role-based authorization (DOCS: Auth is shared plumbing, sec. 01) ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("StudentOnly", p => p.RequireRole(Roles.Student))
    .AddPolicy("StaffOnly", p => p.RequireRole(Roles.Staff))
    .AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin))
    .AddPolicy("ResourceOwner", p => p.AddRequirements(new ResourceOwnerRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// ---- CORS for the React web client and local Flutter dev builds ----
const string CorsPolicy = "StudyHiveClients";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

// ---- Rate limiting on login/refresh (Codex security review, P2: credential-stuffing/brute-force) ----
// 30 requests/minute per client IP. Generous enough that a real user retrying a typo, or this
// project's own test suite hammering /login and /refresh back-to-back, never trips it, while
// still capping an automated password-guessing loop to a fraction of its unthrottled rate.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.AuthEndpoints, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// A wildcard host is fine for local development but must be an explicit allow-list once this is
// reachable from outside localhost — Kestrel's AllowedHosts middleware otherwise accepts any Host
// header, which enables cache-poisoning / password-reset-link style host-header attacks.
if (!builder.Environment.IsDevelopment())
{
    var allowedHosts = builder.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
    {
        throw new InvalidOperationException("AllowedHosts must be an explicit host list outside Development.");
    }
}

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var seedScope = app.Services.CreateScope();
    await DevDataSeeder.SeedAsync(seedScope.ServiceProvider);
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
