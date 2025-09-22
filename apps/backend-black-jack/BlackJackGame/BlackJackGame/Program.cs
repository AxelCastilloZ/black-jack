using BlackJackGame.Extensions;
using BlackJack.Realtime.Hubs;
using BlackJack.Realtime.Extensions;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"[STARTUP-DEBUG] Starting BlackJack application...");
Console.WriteLine($"[STARTUP-DEBUG] Environment: {builder.Environment.EnvironmentName}");

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

Console.WriteLine($"[STARTUP-DEBUG] Adding application services (WITHOUT JWT)...");
// PASO 1: Servicios base SIN JWT (JWT ahora se maneja en SignalR)
builder.Services.AddApplicationServices(builder.Configuration);

Console.WriteLine($"[STARTUP-DEBUG] Adding SignalR with integrated JWT...");
// PASO 2: SignalR con JWT integrado (IConfiguration pasado)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddBlackJackSignalRDevelopment(builder.Configuration);
}
else
{
    builder.Services.AddBlackJackSignalR(builder.Configuration);
}

Console.WriteLine($"[STARTUP-DEBUG] Adding SignalR authorization policies...");
// PASO 3: Políticas de autorización después de JWT
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

Console.WriteLine($"[STARTUP-DEBUG] Application built, configuring pipeline...");

// Migración automática en desarrollo
if (app.Environment.IsDevelopment())
{
    Console.WriteLine($"[STARTUP-DEBUG] Development environment - initializing databases...");
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var identityContext = scope.ServiceProvider.GetRequiredService<BlackJack.Data.Identity.IdentityDbContext>();
            var appContext = scope.ServiceProvider.GetRequiredService<BlackJack.Data.Context.ApplicationDbContext>();

            Console.WriteLine($"[STARTUP-DEBUG] Ensuring identity database created...");
            await identityContext.Database.EnsureCreatedAsync();
            Console.WriteLine($"[STARTUP-DEBUG] Identity database ready");

            Console.WriteLine($"[STARTUP-DEBUG] Ensuring application database created...");
            await appContext.Database.EnsureCreatedAsync();
            Console.WriteLine($"[STARTUP-DEBUG] Application database ready");
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "[STARTUP-DEBUG] Error initializing database");
            Console.WriteLine($"[STARTUP-DEBUG] Database initialization failed: {ex.Message}");
        }
    }

    Console.WriteLine($"[STARTUP-DEBUG] Adding Swagger...");
    app.UseSwagger();
    app.UseSwaggerUI();
}

Console.WriteLine($"[STARTUP-DEBUG] Configuring middleware pipeline...");

app.UseHttpsRedirection();
app.UseCors("DevCors");

Console.WriteLine($"[STARTUP-DEBUG] Adding Authentication middleware...");
// ORDEN CRÍTICO: Authentication antes que Authorization
app.UseAuthentication();

Console.WriteLine($"[STARTUP-DEBUG] Adding Authorization middleware...");
app.UseAuthorization();

Console.WriteLine($"[STARTUP-DEBUG] Mapping controllers...");
app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

Console.WriteLine($"[STARTUP-DEBUG] Mapping SignalR hubs...");
// HUB COORDINADOR PRINCIPAL (compatibilidad)
app.MapHub<GameHub>("/hubs/game");                  // Hub coordinador principal

// HUBS ESPECIALIZADOS
app.MapHub<ConnectionHub>("/hubs/connection");      // Manejo de conexiones y reconexión
app.MapHub<RoomHub>("/hubs/room");                  // Manejo de salas
app.MapHub<SpectatorHub>("/hubs/spectator");        // Manejo de espectadores
app.MapHub<SeatHub>("/hubs/seat");                  // Manejo de asientos
app.MapHub<GameControlHub>("/hubs/game-control");   // Control del juego
app.MapHub<LobbyHub>("/hubs/lobby");                // Hub de lobby (existente)

Console.WriteLine($"[STARTUP-DEBUG] Adding utility endpoints...");
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
    Console.WriteLine($"[STARTUP-DEBUG] Adding debug endpoints...");

    app.MapGet("/api/debug/services", (IServiceProvider services) =>
    {
        Console.WriteLine($"[DEBUG-ENDPOINT] Services check requested");
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
        Console.WriteLine($"[DEBUG-ENDPOINT] Services status: {System.Text.Json.JsonSerializer.Serialize(registeredServices)}");
        return Results.Ok(registeredServices);
    }).AllowAnonymous();

    app.MapGet("/api/debug/jwt-test", (HttpContext context) =>
    {
        Console.WriteLine($"[DEBUG-ENDPOINT] JWT test requested");
        Console.WriteLine($"[DEBUG-ENDPOINT] Path: {context.Request.Path}");
        Console.WriteLine($"[DEBUG-ENDPOINT] Method: {context.Request.Method}");

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        var queryToken = context.Request.Query["access_token"].FirstOrDefault();
        var claims = context.User?.Claims?.Select(c => $"{c.Type}: {c.Value}").ToArray() ?? Array.Empty<string>();

        Console.WriteLine($"[DEBUG-ENDPOINT] Auth header present: {!string.IsNullOrEmpty(authHeader)}");
        Console.WriteLine($"[DEBUG-ENDPOINT] Query token present: {!string.IsNullOrEmpty(queryToken)}");
        Console.WriteLine($"[DEBUG-ENDPOINT] Is authenticated: {context.User?.Identity?.IsAuthenticated ?? false}");
        Console.WriteLine($"[DEBUG-ENDPOINT] Claims count: {claims.Length}");

        var result = new
        {
            HasAuthHeader = !string.IsNullOrEmpty(authHeader),
            HasQueryToken = !string.IsNullOrEmpty(queryToken),
            IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false,
            UserName = context.User?.Identity?.Name,
            Claims = claims,
            Path = context.Request.Path.Value,
            Method = context.Request.Method,
            Timestamp = DateTime.UtcNow
        };

        Console.WriteLine($"[DEBUG-ENDPOINT] Response: {System.Text.Json.JsonSerializer.Serialize(result)}");
        return Results.Ok(result);
    }).RequireAuthorization();

    // Test específico para SignalR auth
    app.MapGet("/api/debug/signalr-token", (HttpContext context) =>
    {
        Console.WriteLine($"[DEBUG-SIGNALR] SignalR token test called");
        var queryToken = context.Request.Query["access_token"].FirstOrDefault();
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        Console.WriteLine($"[DEBUG-SIGNALR] Query token: {(!string.IsNullOrEmpty(queryToken) ? $"Present (length: {queryToken.Length})" : "Not present")}");
        Console.WriteLine($"[DEBUG-SIGNALR] Auth header: {(!string.IsNullOrEmpty(authHeader) ? "Present" : "Not present")}");
        Console.WriteLine($"[DEBUG-SIGNALR] Is authenticated: {context.User?.Identity?.IsAuthenticated ?? false}");

        if (!string.IsNullOrEmpty(queryToken))
        {
            Console.WriteLine($"[DEBUG-SIGNALR] Token preview: {queryToken.Substring(0, Math.Min(50, queryToken.Length))}...");
        }

        var result = new
        {
            QueryTokenPresent = !string.IsNullOrEmpty(queryToken),
            QueryTokenLength = queryToken?.Length ?? 0,
            AuthHeaderPresent = !string.IsNullOrEmpty(authHeader),
            IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false,
            UserName = context.User?.Identity?.Name,
            Timestamp = DateTime.UtcNow
        };

        return Results.Ok(result);
    }).AllowAnonymous();

    // Endpoint específico para debug JWT en SignalR
    app.MapGet("/api/debug/jwt-claims", (HttpContext context) =>
    {
        Console.WriteLine($"[JWT-CLAIMS-DEBUG] JWT claims debug endpoint called");

        var claims = context.User?.Claims?.Select(c => new { Type = c.Type, Value = c.Value }).ToArray() ?? Array.Empty<object>();
        var playerId = context.User?.FindFirst("playerId")?.Value ??
                       context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var name = context.User?.FindFirst("name")?.Value ??
                   context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        Console.WriteLine($"[JWT-CLAIMS-DEBUG] Total claims: {claims.Length}");
        Console.WriteLine($"[JWT-CLAIMS-DEBUG] PlayerId claim: {playerId ?? "NULL"}");
        Console.WriteLine($"[JWT-CLAIMS-DEBUG] Name claim: {name ?? "NULL"}");

        foreach (var claim in claims)
        {
            Console.WriteLine($"[JWT-CLAIMS-DEBUG] Claim: {claim}");
        }

        var result = new
        {
            IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false,
            ClaimsCount = claims.Length,
            Claims = claims,
            PlayerId = playerId,
            Name = name,
            UserName = context.User?.Identity?.Name,
            Timestamp = DateTime.UtcNow
        };

        return Results.Ok(result);
    }).RequireAuthorization();

    // NUEVO: Endpoint para listar todos los hubs disponibles
    app.MapGet("/api/debug/hubs", () =>
    {
        Console.WriteLine($"[DEBUG-HUBS] Hub endpoints requested");
        var hubs = new
        {
            SpecializedHubs = new
            {
                Connection = "/hubs/connection",
                Room = "/hubs/room",
                Spectator = "/hubs/spectator",
                Seat = "/hubs/seat",
                GameControl = "/hubs/game-control"
            },
            ExistingHubs = new
            {
                Lobby = "/hubs/lobby"
            },
            Description = "Specialized hubs for different BlackJack functionalities",
            Timestamp = DateTime.UtcNow
        };

        return Results.Ok(hubs);
    }).AllowAnonymous();
}

Console.WriteLine($"[STARTUP-DEBUG] Application configured with specialized hubs, starting...");
app.Run();