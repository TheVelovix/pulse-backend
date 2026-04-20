using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using pulse.Services;
using pulse.Data;
using pulse_backend.Services;
using UAParser;

var builder = WebApplication.CreateBuilder(args);

DotNetEnv.Env.Load();
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
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
            context.Token = context.Request.Cookies["accessToken"];
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
    {
        var ip = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        return RateLimitPartition.GetFixedWindowLimiter(ip, key => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        })!;
    });
    options.AddFixedWindowLimiter("track", o =>
    {
        o.PermitLimit = 500;
        o.Window = TimeSpan.FromSeconds(10);
    });
});
builder.Services.AddDbContext<MyDbContext>(options =>
{
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_URL"));
});
builder.Services.AddScoped<JwtService>();
builder.Services.AddSingleton(new DatabaseReader("GeoData/GeoLite2-Country.mmdb"));
builder.Services.AddSingleton(Parser.GetDefault());
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://pulse.velovix.com", "pulse.velovix.com", "https://www.pulse.velovix.com")
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    db.Database.Migrate();
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseStaticFiles();
app.MapControllers();
app.Run();
