using BlastStatApi.Parsers;
using BlastStatApi.Services;
//using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Domain services – parser is stateless; service is a singleton cache
builder.Services.AddSingleton<CsgoLogParser>();
builder.Services.AddSingleton<MatchService>();

// Allow the React dev server to call this API
builder.Services.AddCors(options =>
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("DevCors");
app.MapControllers();

// Eager-parse the log on startup so the first request is instant
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<MatchService>().GetMatch();

app.Run();