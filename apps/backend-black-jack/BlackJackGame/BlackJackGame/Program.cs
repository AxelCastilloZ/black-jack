using BlackJackGame.Extensions;
using BlackJack.Realtime.Hubs;
using BlackJack.Realtime.Extensions;
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

// SIMPLIFICADO: Solo llamar AddApplicationServices (ya incluye JWT)
builder.Services.AddApplicationServices(builder.Configuration);

// SignalR según el entorno (esto debe estar FUERA de AddApplicationServices)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddBlackJackSignalRDevelopment();
}
else
{
    builder.Services.AddBlackJackSignalR();
}

// Políticas de autorización para SignalR
builder.Services.AddSignalRAuthorization();

// CORS
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

// Migración automática en desarrollo
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var identityContext = scope.ServiceProvider.GetRequiredService<BlackJack.Data.Identity.IdentityDbContext>();
            var appContext = scope.ServiceProvider.GetRequiredService<BlackJack.Data.Context.ApplicationDbContext>();

            await identityContext.Database.EnsureCreatedAsync();
            await appContext.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error initializing database");
        }
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("DevCors");

// ORDEN CRÍTICO: Authentication antes que Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

// Mapear hubs CON autorización
app.MapHub<GameHub>("/hubs/game");
app.MapHub<LobbyHub>("/hubs/lobby");

// Endpoints de utilidad
app.MapGet("/api/ping", () => Results.Ok(new
{
    ok = true,
    time = DateTime.UtcNow,
    name = "BlackJackGame",
    environment = app.Environment.EnvironmentName
})).AllowAnonymous();

app.MapGet("/api/version", () =>
{
    var v = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    return Results.Ok(new { version = v, buildTime = DateTime.UtcNow });
}).AllowAnonymous();

// Endpoints de debug para desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/debug/services", (IServiceProvider services) =>
    {
        var registeredServices = new
        {
            TableService = services.GetService<BlackJack.Services.Table.ITableService>() != null,
            GameService = services.GetService<BlackJack.Services.Game.IGameService>() != null,
            GameRoomService = services.GetService<BlackJack.Services.Game.IGameRoomService>() != null,
            SignalRService = services.GetService<BlackJack.Realtime.Services.ISignalRNotificationService>() != null,
            ConnectionManager = services.GetService<BlackJack.Realtime.Services.IConnectionManager>() != null,
            EventDispatcher = services.GetService<BlackJack.Services.Common.IDomainEventDispatcher>() != null,
            Timestamp = DateTime.UtcNow
        };
        return Results.Ok(registeredServices);
    }).AllowAnonymous();

    app.MapGet("/api/debug/jwt-test", (HttpContext context) =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        var queryToken = context.Request.Query["access_token"].FirstOrDefault();
        var claims = context.User?.Claims?.Select(c => $"{c.Type}: {c.Value}").ToArray() ?? Array.Empty<string>();

        return Results.Ok(new
        {
            HasAuthHeader = !string.IsNullOrEmpty(authHeader),
            HasQueryToken = !string.IsNullOrEmpty(queryToken),
            IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false,
            UserName = context.User?.Identity?.Name,
            Claims = claims,
            Path = context.Request.Path.Value,
            Method = context.Request.Method,
            Timestamp = DateTime.UtcNow
        });
    }).RequireAuthorization();
}

app.Run();