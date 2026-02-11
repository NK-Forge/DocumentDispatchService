var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseHttpsRedirection();

// Enable Swagger (dev-friendly; fine for now)
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
