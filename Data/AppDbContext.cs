using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Models;

namespace Splitwise_Back.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options):IdentityDbContext(options)
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }
    }
}