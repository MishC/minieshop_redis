using CartService.Models;
using CartService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "cart-service:";
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapCartEndpoints();


app.Run();