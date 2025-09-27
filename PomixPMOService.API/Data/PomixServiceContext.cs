using Microsoft.EntityFrameworkCore;
using ServicePomixPMO.API.Models;

namespace ServicePomixPMO.API.Data
{
    public class PomixServiceContext : DbContext
    {
        public PomixServiceContext(DbContextOptions<PomixServiceContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Request> Request { get; set; }
        public DbSet<Cartable> Cartable { get; set; }
        public DbSet<CartableItem> CartableItems { get; set; }
        public DbSet<UserLog> UserLogs { get; set; }
        public DbSet<RequestLog> RequestLogs { get; set; }
        public DbSet<UserAccess> UserAccesses { get; set; }
        public DbSet<ShahkarLog> ShahkarLog { get; set; } // اضافه شده
        public DbSet<VerifyDocLog> VerifyDocLog { get; set; } // اضافه شده
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToTable("Users", "Sec");
            modelBuilder.Entity<Request>().ToTable("Request", "Define");
            modelBuilder.Entity<Cartable>().ToTable("Cartable", "WF");
            modelBuilder.Entity<CartableItem>().ToTable("CartableItems", "WF");
            modelBuilder.Entity<UserLog>().ToTable("UserLog", "Log");
            modelBuilder.Entity<RequestLog>().ToTable("RequestLogs", "Log");
            modelBuilder.Entity<UserAccess>().ToTable("UserAccess", "Sec");
            modelBuilder.Entity<ShahkarLog>().ToTable("ShahkarLog", "Log"); // اضافه شده
            modelBuilder.Entity<RefreshToken>().ToTable("RefreshTokens", "Sec"); // اضافه شده

            modelBuilder.Entity<User>()
                .HasIndex(u => u.NationalId)
                .IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Request>()
                .HasIndex(r => r.DocumentNumber)
                .IsUnique();
            modelBuilder.Entity<Request>()
                .HasIndex(r => r.NationalId);

            modelBuilder.Entity<Cartable>()
                .HasIndex(c => c.UserId);

            modelBuilder.Entity<CartableItem>()
                .HasIndex(ci => ci.CartableId);
            modelBuilder.Entity<CartableItem>()
                .HasIndex(ci => ci.RequestId);
            modelBuilder.Entity<CartableItem>()
                .HasIndex(ci => ci.AssignedTo);

            modelBuilder.Entity<UserLog>()
                .HasIndex(ul => ul.UserId);
            modelBuilder.Entity<UserLog>()
                .HasIndex(ul => ul.ActionTime);

            modelBuilder.Entity<RequestLog>()
                .HasIndex(rl => rl.RequestId);
            modelBuilder.Entity<RequestLog>()
                .HasIndex(rl => rl.ActionTime);

            modelBuilder.Entity<UserAccess>()
                .HasIndex(ua => new { ua.UserId, ua.Permission })
                .IsUnique();

            modelBuilder.Entity<ShahkarLog>()
                .HasIndex(sl => sl.LogId)
                .IsUnique();

            modelBuilder.Entity<VerifyDocLog>()
                .HasIndex(vf => vf.VerifyDocLogId)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}