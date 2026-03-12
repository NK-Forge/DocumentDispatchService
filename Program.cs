using DocumentDispatchService.Background;
using DocumentDispatchService.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages (Ops UI)
builder.Services.AddRazorPages();
builder.Services.AddSingleton<DocumentDispatchService.Services.OpsActivityLog>();

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

builder.Services.Configure<DispatchWorkerOptions>(
    builder.Configuration.GetSection("DispatchWorker"));

var app = builder.Build();

var migrateOnStartup = builder.Configuration.GetValue<bool>("Database:MigrateOnStartup");

if (migrateOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
    await db.Database.MigrateAsync();
}

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Prometheus: basic request metrics (HTTP count + duration)
app.UseHttpMetrics();

app.MapControllers();

// Ops UI
app.MapRazorPages();

// Prometheus scrape endpoint
app.MapMetrics("/metrics");

app.Run();
