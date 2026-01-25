using MediatR;
using Microsoft.EntityFrameworkCore;
using UserService.Commands;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Commands;

public class UpdateWizardStepHandler : IRequestHandler<UpdateWizardStepCommand, Result<UserProfileDetailDto>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UpdateWizardStepHandler> _logger;

    public UpdateWizardStepHandler(
        ApplicationDbContext context,
        ILogger<UpdateWizardStepHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<UserProfileDetailDto>> Handle(UpdateWizardStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
            
            if (profile == null)
            {
                // Create new profile if doesn't exist
                profile = new UserProfile 
                { 
                    UserId = request.UserId,
                    Email = "" // Will be populated by Keycloak webhook later
                };
                _context.UserProfiles.Add(profile);
            }

            switch (request.Step)
            {
                case 1: // Basic info
                    if (request.BasicInfo == null || !request.BasicInfo.IsValid())
                        return Result<UserProfileDetailDto>.Failure("Invalid basic info - check age requirement (18+)");
                    
                    profile.Name = $"{request.BasicInfo.FirstName} {request.BasicInfo.LastName}";
                    profile.DateOfBirth = request.BasicInfo.DateOfBirth;
                    profile.Gender = request.BasicInfo.Gender;
                    _logger.LogInformation("Updated wizard step 1 for user {UserId}", request.UserId);
                    break;

                case 2: // Preferences
                    if (request.Preferences == null || !request.Preferences.IsValid())
                        return Result<UserProfileDetailDto>.Failure("Invalid preferences - check age/distance settings");
                    
                    // Note: UserProfile model doesn't have MinAge/MaxAge/MaxDistance fields
                    // These would need to be added to the model or stored elsewhere
                    profile.Preferences = request.Preferences.PreferredGender ?? "";
                    profile.Bio = request.Preferences.Bio ?? "";
                    _logger.LogInformation("Updated wizard step 2 for user {UserId}", request.UserId);
                    break;

                case 3: // Photos + completion
                    if (request.Photos == null || !request.Photos.IsValid())
                        return Result<UserProfileDetailDto>.Failure("At least 1 photo required to complete wizard");
                    
                    // Store photo URLs as JSON
                    profile.PhotoUrls = System.Text.Json.JsonSerializer.Serialize(request.Photos.PhotoUrls);
                    profile.PhotoCount = request.Photos.PhotoUrls.Count;
                    if (request.Photos.PhotoUrls.Count > 0)
                    {
                        profile.PrimaryPhotoUrl = request.Photos.PhotoUrls[0];
                    }
                    
                    // Mark wizard complete
                    profile.OnboardingStatus = OnboardingStatus.Ready;
                    profile.OnboardingCompletedAt = DateTime.UtcNow;
                    profile.IsActive = true;
                    _logger.LogInformation("Completed wizard for user {UserId}", request.UserId);
                    break;

                default:
                    return Result<UserProfileDetailDto>.Failure($"Invalid step: {request.Step}");
            }

            await _context.SaveChangesAsync(cancellationToken);
            
            return Result<UserProfileDetailDto>.Success(MapToDetailDto(profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating wizard step {Step} for user {UserId}", request.Step, request.UserId);
            return Result<UserProfileDetailDto>.Failure($"Error updating wizard: {ex.Message}");
        }
    }

    private UserProfileDetailDto MapToDetailDto(UserProfile profile)
    {
        return new UserProfileDetailDto
        {
            Id = profile.Id,
            Name = profile.Name,
            Email = profile.Email,
            Bio = profile.Bio,
            Age = profile.Age,
            Gender = profile.Gender,
            Preferences = profile.Preferences,
            SexualOrientation = profile.SexualOrientation,
            City = profile.City,
            State = profile.State,
            Country = profile.Country,
            PhotoUrls = profile.PhotoUrlList,
            PrimaryPhotoUrl = profile.PrimaryPhotoUrl,
            Occupation = profile.Occupation,
            Company = profile.Company,
            Education = profile.Education,
            School = profile.School,
            Height = profile.Height,
            Religion = profile.Religion,
            Ethnicity = profile.Ethnicity,
            SmokingStatus = profile.SmokingStatus,
            DrinkingStatus = profile.DrinkingStatus,
            WantsChildren = profile.WantsChildren,
            HasChildren = profile.HasChildren,
            RelationshipType = profile.RelationshipType,
            Interests = profile.InterestsList,
            Languages = new List<string>(), // TODO: Parse from JSON
            HobbyList = profile.HobbyList,
            InstagramHandle = profile.InstagramHandle,
            SpotifyTopArtists = profile.SpotifyTopArtists,
            IsVerified = profile.IsVerified,
            IsPhoneVerified = profile.IsPhoneVerified,
            IsEmailVerified = profile.IsEmailVerified,
            IsPhotoVerified = profile.IsPhotoVerified,
            IsPremium = profile.IsPremium,
            SubscriptionType = profile.SubscriptionType,
            CreatedAt = profile.CreatedAt,
            LastActiveAt = profile.LastActiveAt,
            IsOnline = profile.IsOnline,
            OnboardingStatus = profile.OnboardingStatus,
            OnboardingCompletedAt = profile.OnboardingCompletedAt
        };
    }
}
