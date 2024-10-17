using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Models;

namespace Splitwise_Back.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<CustomUser>(options)
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Groups> Groups { get; set; }
        public DbSet<GroupMembers> GroupMembers { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseShare> ExpenseShares { get; set; } 
        public DbSet<UserBalance> UserBalances { get; set; } 

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            builder.Entity<CustomUser>(entity =>
            {
                entity.Property(u => u.ImageUrl).HasMaxLength(256);
            });
            // Group configuration
            builder.Entity<Groups>()
                .HasKey(g => g.Id);
            builder.Entity<Groups>()
                .Property(g => g.Id)
                .ValueGeneratedOnAdd();
            builder.Entity<RefreshToken>()
                .Property(g => g.Id)
                .ValueGeneratedOnAdd();

            builder.Entity<Groups>()
                .HasMany(g => g.GroupMembers)
                .WithOne(gm => gm.Group)
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Groups>()
            .HasOne(g => g.CreatedByUser)
            .WithMany(u => u.CreatedGroups)
            .HasForeignKey(gm => gm.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Groups>()
                .HasMany(g => g.Expenses)
                .WithOne(e => e.Group)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // composit of groupID and UserID as a primary key for the table
            builder.Entity<GroupMembers>()
                .HasKey(gm => new { gm.GroupId, gm.UserId });

            // ExpenseShare Table Composite Key
            builder.Entity<ExpenseShare>()
                .HasKey(es => new { es.ExpenseId, es.UserId });

            // UserBalance Table Composite Key
            builder.Entity<UserBalance>()
                .HasKey(ub => new { ub.UserId, ub.OwedToUserId })
                .IsClustered(false);


            // Relationships for GroupMembers
            builder.Entity<GroupMembers>()
                .HasOne(gm => gm.Group)
                .WithMany(g => g.GroupMembers)
                .HasForeignKey(gm => gm.GroupId);

            builder.Entity<GroupMembers>()
                .HasOne(gm => gm.User)
                .WithMany()
                .HasForeignKey(gm => gm.UserId);

            //Relation for expense share
            builder.Entity<ExpenseShare>()
                .Property(es => es.UserId)
                .HasMaxLength(450);

            builder.Entity<ExpenseShare>()
                .HasOne(es => es.Expense)
                .WithMany(e => e.ExpenseShares)
                .HasForeignKey(es => es.ExpenseId);

            builder.Entity<ExpenseShare>()
                .HasOne(es => es.User)
                .WithMany()
                .HasForeignKey(es => es.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ExpenseShare>()
                .HasOne(es => es.OwesUser)
                .WithMany()
                .HasForeignKey(es => es.OwesUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ExpenseShare>()
                .Property(e => e.AmountOwed)
                .HasPrecision(14, 4);



            // Relationships for UserBalance
            builder.Entity<UserBalance>()
                .HasOne(ub => ub.User)
                .WithMany()
                .HasForeignKey(ub => ub.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<UserBalance>()
                .HasOne(ub => ub.OwedToUser)
                .WithMany()
                .HasForeignKey(ub => ub.OwedToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<UserBalance>()
                .Property(ub => ub.Balance)
                .HasPrecision(14, 4);

            //For Expense
            builder.Entity<Expense>()
                .HasOne(e => e.Group)
                .WithMany(g => g.Expenses)
                .HasForeignKey(e => e.GroupId);

            builder.Entity<Expense>()
                .Property(g => g.Id)
                .ValueGeneratedOnAdd();

            builder.Entity<Expense>()
                .HasOne(e => e.Payer)
                .WithMany()
                .HasForeignKey(e => e.PayerId);

            builder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasPrecision(14, 4);



            builder.Entity<Expense>()
            .HasMany(e => e.ExpenseShares) //one expence can have many ExpenseShare
            .WithOne(es => es.Expense) // ExpenseShare is only associated with one Expence
            .HasForeignKey(e => e.ExpenseId) //Expense has a foreign key expenseId
            .OnDelete(DeleteBehavior.Cascade);

        }

    }
}