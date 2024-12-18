using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;


namespace Splitwise_Back.Data
{
    public static class DataExxtension
    {
        public async static Task InitializeDbAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();

        }

        public static IServiceCollection AddRepositories(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            // Data/GameStoreContext ==> used to connect to the database
            // Repositories => used to register servies under repositories to the application itself
            var connString = configuration.GetConnectionString("Splitwise");
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connString, sqlOptions =>sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(60),
                    errorNumbersToAdd: null)
                );
                options.EnableSensitiveDataLogging(); // Enable sensitive data logging here
            });
            return services;
        }
        
    }
}