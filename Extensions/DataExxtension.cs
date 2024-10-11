using Microsoft.EntityFrameworkCore;

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
            var ConnString = configuration.GetConnectionString("Splitwise");
            services.AddSqlServer<AppDbContext>(ConnString);
            return services;
        }
    }
}