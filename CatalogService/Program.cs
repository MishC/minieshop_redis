using CatalogService.Data;
using CatalogService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "product-service:";
});

builder.Services.AddSingleton<ProductStore>();

var app = builder.Build();
app.Urls.Add("http://0.0.0.0:8080");

app.UseSwagger();
app.UseSwaggerUI();

app.MapProductEndpoints();

app.Run();