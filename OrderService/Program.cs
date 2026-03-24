using OrderService.Endpoints;
using OrderService.Data;
using OrderService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "order-service:";
});

builder.Services.AddSingleton<OrderStore>();
var app = builder.Build();
app.Urls.Add("http://0.0.0.0:8080");

app.UseSwagger();
app.UseSwaggerUI();

app.MapOrderEndpoints();

app.Run();