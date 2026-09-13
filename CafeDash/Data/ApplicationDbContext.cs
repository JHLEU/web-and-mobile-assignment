using CafeDash.Models;
using Microsoft.EntityFrameworkCore;

namespace CafeDash.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<Food> Foods { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentItem> PaymentItems { get; set; }
        public DbSet<ContactUs> ContactUs { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<PasswordReset> PasswordResets { get; set; }

        // Add these two for the raw SQL queries in Bills()
        public DbSet<Bill> Bills { get; set; }
        public DbSet<BillItem> BillItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Restaurant>().ToTable("Restaurants");
            modelBuilder.Entity<Food>().ToTable("Foods");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Payment>().ToTable("Payment");
            modelBuilder.Entity<PaymentItem>().ToTable("Payment_Items");
            modelBuilder.Entity<ContactUs>().ToTable("ContactUs");
            modelBuilder.Entity<Driver>().ToTable("Drivers");
            modelBuilder.Entity<Admin>().ToTable("Admins");
            modelBuilder.Entity<PasswordReset>().ToTable("PasswordResets");

            // Configure Bill and BillItem as keyless types for raw SQL mapping
            modelBuilder.Entity<Bill>().HasNoKey();
            modelBuilder.Entity<BillItem>().HasNoKey();
        }
    }
}