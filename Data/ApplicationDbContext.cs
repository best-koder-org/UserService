// filepath: UserService/Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<MatchPreferences> MatchPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserProfile indexes for query optimization
            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserProfile_UserId");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("IX_UserProfile_Email");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.DateOfBirth)
                .HasDatabaseName("IX_UserProfile_DateOfBirth");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.Gender)
                .HasDatabaseName("IX_UserProfile_Gender");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.City)
                .HasDatabaseName("IX_UserProfile_City");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.State)
                .HasDatabaseName("IX_UserProfile_State");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.Country)
                .HasDatabaseName("IX_UserProfile_Country");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => new { u.Latitude, u.Longitude })
                .HasDatabaseName("IX_UserProfile_Location");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.IsActive)
                .HasDatabaseName("IX_UserProfile_IsActive");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.IsVerified)
                .HasDatabaseName("IX_UserProfile_IsVerified");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.IsOnline)
                .HasDatabaseName("IX_UserProfile_IsOnline");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => u.LastActiveAt)
                .HasDatabaseName("IX_UserProfile_LastActiveAt");

            // Composite indexes for common query patterns
            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => new { u.IsActive, u.Gender, u.DateOfBirth })
                .HasDatabaseName("IX_UserProfile_Search_Common");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(u => new { u.IsActive, u.LastActiveAt })
                .HasDatabaseName("IX_UserProfile_Active_LastActive");

            // MatchPreferences indexes
            modelBuilder.Entity<MatchPreferences>()
                .HasIndex(m => m.UserId)
                .IsUnique()
                .HasDatabaseName("IX_MatchPreferences_UserId");

            modelBuilder.Entity<MatchPreferences>()
                .HasIndex(m => m.UserProfileId)
                .HasDatabaseName("IX_MatchPreferences_UserProfileId");
        }
    }
}