using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "gateway:";
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrEmpty(context.Request.Headers.Authorization))
                {
                    return Task.CompletedTask;
                }

                var cookieName = context.HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["AuthCookie:Name"] ?? "access_token";
                context.Token = context.Request.Cookies[cookieName];
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var sessionId = GetSessionId(context.Principal!);
                if (string.IsNullOrEmpty(sessionId))
                {
                    context.Fail("Session id is missing.");
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                if (string.IsNullOrEmpty(await cache.GetStringAsync(GetSessionKey(sessionId))))
                {
                    context.Fail("Session is no longer active.");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


var app = builder.Build();

app.Urls.Add("http://0.0.0.0:8080");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/register", async (AuthRequest request, IDistributedCache cache, HttpContext httpContext) =>
{
    var email = NormalizeEmail(request.Email);
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
    {
        return Results.BadRequest("Email is required and password must have at least 8 characters.");
    }

    var userKey = GetUserKey(email);
    if (!string.IsNullOrEmpty(await cache.GetStringAsync(userKey)))
    {
        return Results.Conflict("User already exists.");
    }

    var user = new StoredUser(Guid.NewGuid().ToString("N"), email, HashPassword(request.Password), DateTime.UtcNow);
    await cache.SetStringAsync(userKey, JsonSerializer.Serialize(user));

    var sessionId = Guid.NewGuid().ToString("N");
    var token = CreateAccessToken(user, sessionId, app.Configuration, out var expiresAtUtc);
    await SaveSessionAsync(cache, user, sessionId, expiresAtUtc);

    SetAccessTokenCookie(httpContext, app.Configuration, token, expiresAtUtc);
    return Results.Ok(new AuthResponse(user.Id, user.Email, sessionId, expiresAtUtc));
});

app.MapPost("/auth/login", async (AuthRequest request, IDistributedCache cache, HttpContext httpContext) =>
{
    var email = NormalizeEmail(request.Email);
    var cachedUser = await cache.GetStringAsync(GetUserKey(email));
    if (string.IsNullOrEmpty(cachedUser))
    {
        return Results.Unauthorized();
    }

    var user = JsonSerializer.Deserialize<StoredUser>(cachedUser);
    if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var sessionId = Guid.NewGuid().ToString("N");
    var token = CreateAccessToken(user, sessionId, app.Configuration, out var expiresAtUtc);
    await SaveSessionAsync(cache, user, sessionId, expiresAtUtc);

    SetAccessTokenCookie(httpContext, app.Configuration, token, expiresAtUtc);
    return Results.Ok(new AuthResponse(user.Id, user.Email, sessionId, expiresAtUtc));
});

app.MapGet("/auth/me", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
        email = user.FindFirstValue(ClaimTypes.Email),
        sessionId = GetSessionId(user)
    });
}).RequireAuthorization();

app.MapPost("/auth/logout", async (HttpContext httpContext, IDistributedCache cache, ClaimsPrincipal user) =>
{
    var sessionId = GetSessionId(user);
    if (!string.IsNullOrEmpty(sessionId))
    {
        await cache.RemoveAsync(GetSessionKey(sessionId));
    }

    httpContext.Response.Cookies.Delete(GetAuthCookieName(app.Configuration), GetAuthCookieOptions(app.Configuration, DateTime.UtcNow));
    return Results.NoContent();
}).RequireAuthorization();

app.MapReverseProxy();




app.Run();

static string NormalizeEmail(string email)
{
    return email.Trim().ToLowerInvariant();
}

static string GetUserKey(string email)
{
    return $"auth:user:{email}";
}

static string HashPassword(string password)
{
    const int iterations = 100_000;
    var salt = RandomNumberGenerator.GetBytes(16);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
}

static bool VerifyPassword(string password, string storedHash)
{
    var parts = storedHash.Split('.');
    if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
    {
        return false;
    }

    var salt = Convert.FromBase64String(parts[1]);
    var expectedHash = Convert.FromBase64String(parts[2]);
    var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

    return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
}

static string GetSessionKey(string sessionId)
{
    return $"auth:session:{sessionId}";
}

static async Task SaveSessionAsync(IDistributedCache cache, StoredUser user, string sessionId, DateTime expiresAtUtc)
{
    var session = new StoredSession(sessionId, user.Id, user.Email, DateTime.UtcNow, expiresAtUtc);

    await cache.SetStringAsync(
        GetSessionKey(sessionId),
        JsonSerializer.Serialize(session),
        new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiresAtUtc
        });
}

static string CreateAccessToken(StoredUser user, string sessionId, IConfiguration configuration, out DateTime expiresAtUtc)
{
    var jwt = configuration.GetSection("Jwt");
    expiresAtUtc = DateTime.UtcNow.AddMinutes(jwt.GetValue("AccessTokenMinutes", 60));

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id),
        new Claim(JwtRegisteredClaimNames.Sid, sessionId),
        new Claim(ClaimTypes.Sid, sessionId),
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Email, user.Email)
    };

    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwt["Issuer"],
        audience: jwt["Audience"],
        claims: claims,
        expires: expiresAtUtc,
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

static void SetAccessTokenCookie(HttpContext httpContext, IConfiguration configuration, string token, DateTime expiresAtUtc)
{
    httpContext.Response.Cookies.Append(GetAuthCookieName(configuration), token, GetAuthCookieOptions(configuration, expiresAtUtc));
}

static string GetAuthCookieName(IConfiguration configuration)
{
    return configuration["AuthCookie:Name"] ?? "access_token";
}

static CookieOptions GetAuthCookieOptions(IConfiguration configuration, DateTime expiresAtUtc)
{
    return new CookieOptions
    {
        HttpOnly = true,
        Secure = configuration.GetValue("AuthCookie:Secure", true),
        SameSite = ParseSameSiteMode(configuration["AuthCookie:SameSite"]),
        Expires = expiresAtUtc,
        Path = "/"
    };
}

static SameSiteMode ParseSameSiteMode(string? value)
{
    return Enum.TryParse<SameSiteMode>(value, ignoreCase: true, out var sameSiteMode)
        ? sameSiteMode
        : SameSiteMode.Lax;
}

static string? GetSessionId(ClaimsPrincipal user)
{
    return user.FindFirstValue(JwtRegisteredClaimNames.Sid)
        ?? user.FindFirstValue(ClaimTypes.Sid);
}

public record AuthRequest(string Email, string Password);
public record AuthResponse(string UserId, string Email, string SessionId, DateTime ExpiresAtUtc);
public record StoredUser(string Id, string Email, string PasswordHash, DateTime CreatedAtUtc);
public record StoredSession(string Id, string UserId, string Email, DateTime CreatedAtUtc, DateTime ExpiresAtUtc);

