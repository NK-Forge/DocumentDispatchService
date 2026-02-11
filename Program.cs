using DocumentDispatchService.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<DispatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters
        .Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseHttpsRedirection();

// Enable Swagger (dev-friendly; fine for now)
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
