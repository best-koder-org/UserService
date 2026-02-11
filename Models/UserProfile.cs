// filepath: UserService/Models/UserProfile.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models
{
    [Table("UserProfiles")]
    public class UserProfile
    {
        public int Id { get; set; }

        /// <summary>
        /// Keycloak user ID - links profile to auth identity
        /// </summary>
        public Guid UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty; // Unique identifier for login and linking

        [StringLength(1000)]
        public string Bio { get; set; } = string.Empty;

        [StringLength(500)]
        public string ProfilePictureUrl { get; set; } = string.Empty;

        [StringLength(50)]
        public string Preferences { get; set; } = string.Empty; // e.g., "Men, Women, Both"

        [StringLength(50)]
        public string Gender { get; set; } = string.Empty; // e.g., "Male", "Female", "Non-binary"

        [StringLength(50)]
        public string SexualOrientation { get; set; } = string.Empty; // e.g., "Straight", "Gay", "Bisexual"

        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; } // For age calculation

        // Enhanced location fields
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [Column(TypeName = "decimal(9,6)")]
        public double Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public double Longitude { get; set; }

        [StringLength(100)]
        public string Location { get; set; } = string.Empty; // Legacy field for compatibility

        // Photo management
        [StringLength(2000)]
        public string PhotoUrls { get; set; } = string.Empty; // JSON array of photo URLs

        public int PhotoCount { get; set; } = 0;

        [StringLength(500)]
        public string PrimaryPhotoUrl { get; set; } = string.Empty;

        // Professional information
        [StringLength(100)]
        public string Occupation { get; set; } = string.Empty;

        [StringLength(100)]
        public string Company { get; set; } = string.Empty;

        [StringLength(100)]
        public string Education { get; set; } = string.Empty;

        [StringLength(100)]
        public string School { get; set; } = string.Empty;

        // Personal attributes
        public int Height { get; set; } // in cm

        [StringLength(50)]
        public string Religion { get; set; } = string.Empty;

        [StringLength(50)]
        public string Ethnicity { get; set; } = string.Empty;

        [StringLength(50)]
        public string SmokingStatus { get; set; } = string.Empty; // Never, Sometimes, Often

        [StringLength(50)]
        public string DrinkingStatus { get; set; } = string.Empty; // Never, Sometimes, Often

        public bool WantsChildren { get; set; }
        public bool HasChildren { get; set; }

        [StringLength(50)]
        public string RelationshipType { get; set; } = string.Empty; // Casual, Serious, Both

        // Interests and lifestyle
        [StringLength(2000)]
        public string Interests { get; set; } = string.Empty; // JSON array of interests

        [StringLength(2000)]
        public string Languages { get; set; } = string.Empty; // JSON array of languages

        [StringLength(500)]
        public string HobbyList { get; set; } = string.Empty;

        // Social media integration
        [StringLength(100)]
        public string InstagramHandle { get; set; } = string.Empty;

        [StringLength(100)]
        public string SpotifyId { get; set; } = string.Empty;

        [StringLength(1000)]
        public string SpotifyTopArtists { get; set; } = string.Empty;

        [StringLength(1000)]
        public string SpotifyTopTracks { get; set; } = string.Empty;

        // Verification and trust
        public bool IsVerified { get; set; } = false;
        public bool IsPhoneVerified { get; set; } = false;
        public bool IsEmailVerified { get; set; } = false;
        public bool IsPhotoVerified { get; set; } = false;

        [StringLength(500)]
        public string VerificationPhotoUrl { get; set; } = string.Empty;

        public DateTime? VerificationDate { get; set; }

        // Privacy and safety
        public bool IsPrivate { get; set; } = false;
        public bool ShowAge { get; set; } = true;
        public bool ShowDistance { get; set; } = true;
        public bool ShowOnlineStatus { get; set; } = true;
        public bool AllowMessageFromMatches { get; set; } = true;
        public bool AllowMessageFromEveryone { get; set; } = false;

        // Premium features
        public bool IsPremium { get; set; } = false;
        public DateTime? PremiumExpiry { get; set; }

        [StringLength(50)]
        public string SubscriptionType { get; set; } = string.Empty; // Basic, Plus, Gold

        // Activity tracking
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLocationUpdate { get; set; }

        // Onboarding wizard progress
        public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.Incomplete;
        public DateTime? OnboardingCompletedAt { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsOnline { get; set; } = false;

        // Calculated fields
        [NotMapped]
        public int Age
        {
            get
            {
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Year;
                if (DateOfBirth.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        [NotMapped]
        public List<string> PhotoUrlList
        {
            get
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(PhotoUrls ?? "[]") ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }

        [NotMapped]
        public List<string> InterestsList
        {
            get
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(Interests ?? "[]") ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }
    }
}