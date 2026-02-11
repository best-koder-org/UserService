using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs
{
    public class CreateUserProfileDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Bio { get; set; } = string.Empty;

        [Required]
        public string Gender { get; set; } = string.Empty;

        [Required]
        public string Preferences { get; set; } = string.Empty;

        [Required]
        public DateTime DateOfBirth { get; set; }

        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string Occupation { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public List<string> Interests { get; set; } = new();
        public List<string> Languages { get; set; } = new();

        public int Height { get; set; }
        public string Religion { get; set; } = string.Empty;
        public string SmokingStatus { get; set; } = string.Empty;
        public string DrinkingStatus { get; set; } = string.Empty;
        public bool WantsChildren { get; set; }
        public bool HasChildren { get; set; }
        public string RelationshipType { get; set; } = string.Empty;
    }

    public class UpdateUserProfileDto
    {
        public string? Name { get; set; }
        public string? Bio { get; set; }
        public string? Occupation { get; set; }
        public string? Company { get; set; }
        public string? Education { get; set; }
        public string? School { get; set; }
        public int? Height { get; set; }
        public string? Religion { get; set; }
        public string? Ethnicity { get; set; }
        public string? SmokingStatus { get; set; }
        public string? DrinkingStatus { get; set; }
        public bool? WantsChildren { get; set; }
        public bool? HasChildren { get; set; }
        public string? RelationshipType { get; set; }
        public List<string>? Interests { get; set; }
        public List<string>? Languages { get; set; }
        public string? HobbyList { get; set; }
        public string? InstagramHandle { get; set; }
    }

    public class LocationUpdateDto
    {
        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    public class PhotoUploadDto
    {
        [Required]
        public IFormFile Photo { get; set; } = null!;

        public bool IsPrimary { get; set; } = false;
        public string Description { get; set; } = string.Empty;
    }

    public class PhotoResponseDto
    {
        public string PhotoUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class PrivacySettingsDto
    {
        public bool ShowAge { get; set; } = true;
        public bool ShowDistance { get; set; } = true;
        public bool ShowOnlineStatus { get; set; } = true;
        public bool AllowMessageFromMatches { get; set; } = true;
        public bool AllowMessageFromEveryone { get; set; } = false;
        public bool IsPrivate { get; set; } = false;
    }

    public class VerificationRequestDto
    {
        [Required]
        public IFormFile VerificationPhoto { get; set; } = null!;

        public string VerificationType { get; set; } = "photo"; // photo, phone, email
    }

    public class UserProfileSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string City { get; set; } = string.Empty;
        public string PrimaryPhotoUrl { get; set; } = string.Empty;
        public List<string> PhotoUrls { get; set; } = new();
        public string Bio { get; set; } = string.Empty;
        public string Occupation { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public int Height { get; set; }
        public string Gender { get; set; } = string.Empty;
        public List<string> Interests { get; set; } = new();
        public List<PromptAnswer> Prompts { get; set; } = new();
        public string? VoicePromptUrl { get; set; }
        public bool IsVerified { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastActiveAt { get; set; }
        public double? Distance { get; set; }
    }

    public class PromptAnswer
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }

    public class UserProfileDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Convenience properties for tests that expect firstName/lastName  
        public string FirstName => Name.Split(' ', 2).FirstOrDefault() ?? "";
        public string LastName => Name.Split(' ', 2).Skip(1).FirstOrDefault() ?? "";

        public string Email { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Preferences { get; set; } = string.Empty;
        public string SexualOrientation { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public List<string> PhotoUrls { get; set; } = new();
        public string PrimaryPhotoUrl { get; set; } = string.Empty;

        public string Occupation { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string School { get; set; } = string.Empty;

        public int Height { get; set; }
        public string Religion { get; set; } = string.Empty;
        public string Ethnicity { get; set; } = string.Empty;
        public string SmokingStatus { get; set; } = string.Empty;
        public string DrinkingStatus { get; set; } = string.Empty;
        public bool WantsChildren { get; set; }
        public bool HasChildren { get; set; }
        public string RelationshipType { get; set; } = string.Empty;

        public List<string> Interests { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public string HobbyList { get; set; } = string.Empty;

        public string InstagramHandle { get; set; } = string.Empty;
        public string SpotifyTopArtists { get; set; } = string.Empty;

        public bool IsVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhotoVerified { get; set; }

        public bool IsPremium { get; set; }
        public string SubscriptionType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public bool IsOnline { get; set; }

        // Onboarding wizard status
        public Models.OnboardingStatus OnboardingStatus { get; set; }
        public DateTime? OnboardingCompletedAt { get; set; }
    }

    public class SearchUsersDto
    {
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public string? Gender { get; set; }
        public double? MaxDistance { get; set; }
        public List<string>? Interests { get; set; }
        public string? Education { get; set; }
        public string? Location { get; set; }
        public bool? IsVerified { get; set; }
        public bool? IsOnline { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "lastactive"; // lastactive, age, distance, verified
        public string SortOrder { get; set; } = "desc"; // asc, desc
    }

    public class SearchResultDto<T>
    {
        public List<T> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }
    }
}
