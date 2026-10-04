using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using pulse.Data;
using pulse.Helpers;
using pulse.Middleware;
using pulse.Services;
using UAParser;

var builder = WebApplication.CreateBuilder(args);

DotNetEnv.Env.Load();
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET")!)),
            ValidateIssuer = true,
            ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
            ValidateAudience = true,
            ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
            ValidateLifetime = true
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["accessToken"];
                if (string.IsNullOrEmpty(token))
                    token = context.Request.Query["token"];
                context.Token = token;
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ApiKey", policy => policy
        .AddAuthenticationSchemes("ApiKey")
        .RequireAuthenticatedUser());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
    {
        var ip = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.AddFixedWindowLimiter("track", o =>
    {
        o.PermitLimit = 500;
        o.Window = TimeSpan.FromSeconds(10);
    });

    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromSeconds(10);
        o.QueueLimit = 0;
    });

    options.AddPolicy("favicon", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddDbContext<MyDbContext>(options =>
{
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_URL"));
});

// Favicon proxy
builder.Services.AddMemoryCache(o => o.SizeLimit = 5000);
builder.Services.AddScoped<FaviconService>();
builder.Services.AddHttpClient("favicon")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(3),
        ConnectCallback = async (ctx, ct) =>
        {
            var addrs = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
            if (addrs.Length == 0 || addrs.Any(IsPrivate))
                throw new HttpRequestException("Blocked address");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addrs, ctx.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    });

builder.Services.AddHttpClient();

builder.Services.AddScoped<JwtService>();
builder.Services.AddSingleton(new DatabaseReader("GeoData/GeoLite2-Country.mmdb"));
builder.Services.AddSingleton(Parser.GetDefault());
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://pulse.velovix.com", "https://www.pulse.velovix.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
    options.AddPolicy("tracker", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<PaddleService>();
builder.Services.AddHostedService<DataRetentionService>();
builder.Services.AddSingleton<TurnstileService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddHostedService<WeeklyReportService>();
builder.Services.AddHostedService<BundledSubscriptionService>();
builder.Services.AddHostedService<EmailVerificationCodesService>();
builder.Services.AddSingleton<ActiveVisitorService>();
builder.Services.AddScoped<Utils>();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Must be first so the real client IP is visible to everything below
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseResponseCompression();
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/api/favicon", async (string domain, FaviconService svc, HttpContext http, CancellationToken ct) =>
{
    if (!FaviconService.IsValidDomain(domain)) return Results.BadRequest();

    var entry = await svc.GetAsync(domain, ct);
    if (entry is null) return Results.NotFound();

    http.Response.Headers.CacheControl = "public, max-age=604800";
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(entry.Data, entry.ContentType);
}).RequireRateLimiting("favicon");

app.Run();

static bool IsPrivate(IPAddress ip)
{
    if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
    if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;

    var b = ip.GetAddressBytes();
    if (ip.AddressFamily == AddressFamily.InterNetwork)
    {
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 0;
    }

    return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6Multicast;
}
