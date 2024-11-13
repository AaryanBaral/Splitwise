
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Splitwise_Back.Configurations;
using Splitwise_Back.Data;
using Splitwise_Back.Data.Transaction;
using Splitwise_Back.ExceptionHandler;
using Splitwise_Back.Models;
using Splitwise_Back.Services.Expense;
using Splitwise_Back.Services.ExternalServices;
using Splitwise_Back.Services.Group;
using Splitwise_Back.Services.User;
using Splitwise_Back.Services.UserBalance;
using Splitwise_Back.Validation.Group;

namespace Splitwise_Back.Extensions
{
    public static class ServiceExtensionMethod
    {
        public static void AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle


            // adding the Jwt token service
            services.AddScoped<ITokenService, JwtTokenService>();

            /* 
                Add Jwt configuration to the builder DI
                It basically automatically maps the "JwtConfig" section in the app settings.json file 
                to the JwtConfig class 
                you cant directly access the secret from jwt class instance 
                you have to use IOption<JwtConfig> instance.Value to access the actual values inside of that class
            */
            services.Configure<JwtConfig>(configuration.GetSection("JwtConfig"));
            services.AddJwtAuthentication(configuration);
            services.AddIdentityConfiguration();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
            
            // services.AddSingleton<EmailService>();
            services.AddScoped<IExpenseService,ExpenseService>();
            
            services.AddScoped<IGroupService,GroupService>();
            
            services.AddScoped<IUserService,UserService>();
            services.AddScoped<IUserBalanceService,UserBalanceService>();
            
            services.AddScoped<GroupValidation>();
        }

        private static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var secret = configuration.GetSection("JwtConfig:Secret").Value;
            if (string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException("JWT secret is missing in configuration");
            }
            var key = Encoding.ASCII.GetBytes(secret);
            var tokenValidationParameters = new TokenValidationParameters()
            {
                //used to validate token using different options
                ValidateIssuerSigningKey = true, //to validate the tokens signing key
                IssuerSigningKey = new SymmetricSecurityKey(key), // we compare if it matches our key or not
                ValidateIssuer = false, // it issued to validate the issuer
                ValidateAudience = false, // it issued to validate the issuer
                RequireExpirationTime = false, //it sets the token is not expired 
                ValidateLifetime = true // it sets that the token is valid for lifetime
            };

            // Add the Authentication scheme and configurations
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            // Add the jwt configurations as what should be done and how to do it
            .AddJwtBearer(jwt =>
            {
                jwt.SaveToken = true; // saves the generated token to http context
                jwt.TokenValidationParameters = tokenValidationParameters;
            });
            services.AddSingleton(tokenValidationParameters);
        }

        /// Configures ASP.NET Core Identity.
        private static void AddIdentityConfiguration(this IServiceCollection services)
        {
            services.AddIdentity<CustomUsers,IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<AppDbContext>();
        }
    }
}