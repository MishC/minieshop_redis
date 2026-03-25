using OrderService.Endpoints;
using OrderService.Data;
using OrderService.Models;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "order-service:";
});

builder.Services.AddHttpClient("CartApi", client =>
{
    client.BaseAddress = new Uri("http://cartservice:8080"); //http://localhost:5002
});

builder.Services.AddHttpClient("CatalogApi", client =>
{
    client.BaseAddress = new Uri("http://catalogservice:8080"); //http://localhost:5001
});
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
//builder.Services.AddSingleton<OrderStore>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.Migrate();
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapOrderEndpoints();

app.Run();