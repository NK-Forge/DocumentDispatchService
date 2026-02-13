using DocumentDispatchService.Background;
using DocumentDispatchService.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<DispatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters
        .Add(new JsonStringEnumConverter()));

builder.Services.AddHostedService<DispatchWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Prometheus: basic request metrics (HTTP count + duration)
app.UseHttpMetrics();

app.MapControllers();

// Prometheus scrape endpoint
app.MapMetrics("/metrics");

app.Run();
