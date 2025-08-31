using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using black_jack_backend.Data; 
namespace black_jack_backend.Modules;

public static class AppModule
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add DbContext with TypeORM-style configuration
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
        });

        // Auto-load entities (equivalent to autoLoadEntities: true)
        services.AddScoped<ApplicationDbContext>();
        services.AddUsersModule();
        services.AddRoomsModule();
        
        return services;
    }
}