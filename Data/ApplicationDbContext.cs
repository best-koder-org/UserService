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
        public DbSet<NotificationPreferences> NotificationPreferences { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<Entitlement> Entitlements { get; set; }
        public DbSet<SparksLedgerEntry> SparksLedger { get; set; }
        public DbSet<SparkRecord> Sparks { get; set; }

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
                .HasIndex(u => u.AccountStatus)
                .HasDatabaseName("IX_UserProfile_AccountStatus");

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

            // NotificationPreferences indexes
            modelBuilder.Entity<NotificationPreferences>()
                .HasIndex(n => n.UserId)
                .IsUnique()
                .HasDatabaseName("IX_NotificationPreferences_UserId");

            modelBuilder.Entity<NotificationPreferences>()
                .HasIndex(n => n.UserProfileId)
                .HasDatabaseName("IX_NotificationPreferences_UserProfileId");

            // SupportTicket configuration (T091)
            modelBuilder.Entity<SupportTicket>()
                .HasIndex(t => t.TicketId)
                .IsUnique()
                .HasDatabaseName("IX_SupportTicket_TicketId");

            modelBuilder.Entity<SupportTicket>()
                .HasIndex(t => t.UserId)
                .HasDatabaseName("IX_SupportTicket_UserId");

            modelBuilder.Entity<SupportTicket>()
                .Property(t => t.Category)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<SupportTicket>()
                .Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Entitlement (P1.1)
            modelBuilder.Entity<Entitlement>()
                .HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Entitlement_UserId");

            modelBuilder.Entity<Entitlement>()
                .Property(e => e.Tier)
                .HasConversion<string>()
                .HasMaxLength(20);

            // SparksLedger (P1.1)
            modelBuilder.Entity<SparksLedgerEntry>()
                .HasIndex(s => s.UserId)
                .HasDatabaseName("IX_SparksLedger_UserId");
        }
    }
}