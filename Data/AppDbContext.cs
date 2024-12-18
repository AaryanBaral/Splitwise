using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Models;

namespace Splitwise_Back.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<CustomUsers>(options)
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Groups> Groups { get; set; }
        public DbSet<GroupMembers> GroupMembers { get; set; }
        public DbSet<Expenses> Expenses { get; set; }
        public DbSet<ExpenseShares> ExpenseShares { get; set; }
        public DbSet<UserBalances> UserBalances { get; set; }
        public DbSet<ExpensePayers> ExpensePayers { get; set; }
        public DbSet<Settlement> Settlements { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<CustomUsers>(entity => { entity.Property(u => u.ImageUrl).HasMaxLength(256); });
            // Group configuration
            builder.Entity<Groups>()
                .HasKey(g => g.Id);
            builder.Entity<Groups>()
                .Property(g => g.Id)
                .ValueGeneratedOnAdd();
            builder.Entity<RefreshToken>()
                .Property(g => g.Id)
                .ValueGeneratedOnAdd();
            builder.Entity<ExpensePayers>()
                .Property(ep => ep.AmountPaid)
                .HasPrecision(28, 10);

            builder.Entity<Settlement>()
                .HasKey(s => s.SettlementId);

            builder.Entity<Settlement>()
                .HasOne(s => s.Group)
                .WithMany()
                .HasForeignKey(s => s.GroupId);

            builder.Entity<Settlement>()
                .HasOne(s => s.Payer)
                .WithMany()
                .HasForeignKey(s => s.PayerId)
                .OnDelete(DeleteBehavior.Cascade);;

            builder.Entity<Settlement>()
                .HasOne(s => s.Receiver)
                .WithMany()
                .HasForeignKey(s => s.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Settlement>()
                .Property(s => s.Amount)
                .HasPrecision(28, 10);


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
            builder.Entity<ExpenseShares>()
                .HasKey(es => new { es.ExpenseId, es.OwedByUserId, es.OwesToUserId });

            // UserBalance Table Composite Key
            builder.Entity<UserBalances>()
                .HasKey(ub => new { ub.OwedByUserId, ub.OwesToUserId, ub.GroupId })
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
            builder.Entity<ExpenseShares>()
                .Property(es => es.OwedByUserId)
                .HasMaxLength(450);

            builder.Entity<ExpenseShares>()
                .HasOne(es => es.Expense)
                .WithMany(e => e.ExpenseShares)
                .HasForeignKey(es => es.ExpenseId);

            builder.Entity<ExpenseShares>()
                .HasOne(es => es.OwedByUser)
                .WithMany()
                .HasForeignKey(es => es.OwedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ExpenseShares>()
                .HasOne(es => es.OwesToUser)
                .WithMany()
                .HasForeignKey(es => es.OwesToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ExpenseShares>()
                .Property(e => e.AmountOwed)
                .HasPrecision(28, 10);


            // Relationships for UserBalance
            builder.Entity<UserBalances>()
                .HasOne(ub => ub.OwedByUser)
                .WithMany()
                .HasForeignKey(ub => ub.OwedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<UserBalances>()
                .HasOne(ub => ub.OwesToUser)
                .WithMany()
                .HasForeignKey(ub => ub.OwesToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<UserBalances>()
                .HasOne(ub => ub.Group)
                .WithMany(g => g.UserBalances)
                .HasForeignKey(ub => ub.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<UserBalances>()
                .Property(ub => ub.Balance)
                .HasPrecision(28, 10);

            //For Expense
            builder.Entity<Expenses>()
                .HasOne(e => e.Group)
                .WithMany(g => g.Expenses)
                .HasForeignKey(e => e.GroupId);

            builder.Entity<Expenses>()
                .Property(g => g.Id)
                .ValueGeneratedOnAdd();

            builder.Entity<Expenses>()
                .HasMany(e => e.Payers)
                .WithOne(p => p.Expense)
                .HasForeignKey(p => p.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Expenses>()
                .Property(e => e.Amount)
                .HasPrecision(28, 10);


            builder.Entity<Expenses>()
                .HasMany(e => e.ExpenseShares) //one expense can have many ExpenseShare
                .WithOne(es => es.Expense) // ExpenseShare is only associated with one Expense
                .HasForeignKey(e => e.ExpenseId) //Expense share has a foreign key expenseId
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ExpensePayers>()
                .HasOne(ep => ep.Expense)
                .WithMany(e => e.Payers)
                .HasForeignKey(p => p.ExpenseId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}