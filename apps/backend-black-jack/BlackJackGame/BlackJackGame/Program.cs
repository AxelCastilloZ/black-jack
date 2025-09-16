using BlackJackGame.Extensions;
using BlackJack.Realtime.Hubs;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "BlackJackGame", Version = "v1" });
    var jwt = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Token JWT. Ej: Bearer {token}"
    };
    o.AddSecurityDefinition("Bearer", jwt);
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwt] = Array.Empty<string>() });
});

// Health + CORS
builder.Services.AddHealthChecks();
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("DevCors", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
         .WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:3000",
            "https://localhost:5173",
            "https://localhost:5174",
            "https://localhost:3000"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

app.MapHub<GameHub>("/hubs/game");
app.MapHub<LobbyHub>("/hubs/lobby");

app.MapGet("/api/ping", () => Results.Ok(new { ok = true, time = DateTime.UtcNow, name = "BlackJackGame" }))
   .AllowAnonymous();

app.MapGet("/api/version", () =>
{
    var v = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    return Results.Ok(new { version = v });
}).AllowAnonymous();

app.Run();
