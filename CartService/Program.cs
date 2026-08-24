using CartService.Models;
using CartService.Endpoints;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "cart-service:";
});

builder.Services.AddHttpClient("CatalogApi", client =>
{
    client.BaseAddress = new Uri("http://catalogservice:8080");
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
                var sessionId = context.Principal?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sid)
                    ?? context.Principal?.FindFirstValue(System.Security.Claims.ClaimTypes.Sid);

                if (string.IsNullOrEmpty(sessionId))
                {
                    context.Fail("Session id is missing.");
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                if (string.IsNullOrEmpty(await cache.GetStringAsync($"auth:session:{sessionId}")))
                {
                    context.Fail("Session is no longer active.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapCartEndpoints();


app.Run();
