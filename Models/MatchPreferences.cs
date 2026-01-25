using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models;

/// <summary>
/// Match preferences for dating algorithm
/// Controls who appears in user's discovery queue
/// </summary>
[Table("MatchPreferences")]
public class MatchPreferences
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to UserProfile
    /// </summary>
    public int UserProfileId { get; set; }
    
    /// <summary>
    /// Keycloak user ID for direct lookup
    /// </summary>
    public Guid UserId { get; set; }
    
    // Age preferences
    [Range(18, 100)]
    public int MinAge { get; set; } = 18;
    
    [Range(18, 100)]
    public int MaxAge { get; set; } = 35;
    
    // Distance preferences
    [Range(1, 500)]
    public int MaxDistanceKm { get; set; } = 50;
    
    // Gender preferences
    [StringLength(50)]
    public string PreferredGender { get; set; } = "Any"; // Men, Women, Non-binary, Any
    
    // Relationship type preferences
    [StringLength(500)]
    public string RelationshipGoals { get; set; } = string.Empty; // JSON array: ["Long-term", "Short-term", "Friendship"]
    
    // Deal-breakers
    public bool DealBreakerSmoking { get; set; } = false; // If true, exclude smokers
    public bool DealBreakerDrinking { get; set; } = false;
    public bool DealBreakerHasChildren { get; set; } = false;
    public bool DealBreakerWantsChildren { get; set; } = false;
    
    // Religion matching
    public bool RequireSameReligion { get; set; } = false;
    
    // Education matching
    public bool PreferSimilarEducation { get; set; } = false;
    
    // Show me in discovery
    public bool ShowMeInDiscovery { get; set; } = true;
    
    // Discovery limits
    public bool EnableDailyLimit { get; set; } = true;
    
    // Advanced filters (premium feature)
    [Range(0, 300)]
    public int? MinHeightCm { get; set; }
    
    [Range(0, 300)]
    public int? MaxHeightCm { get; set; }
    
    [StringLength(500)]
    public string PreferredEthnicities { get; set; } = string.Empty; // JSON array
    
    [StringLength(1000)]
    public string MustHaveInterests { get; set; } = string.Empty; // JSON array of required interests
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    [ForeignKey("UserProfileId")]
    public UserProfile? UserProfile { get; set; }
}
