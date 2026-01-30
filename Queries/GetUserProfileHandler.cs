using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Queries;

/// <summary>
/// Handles GetUserProfileQuery
/// Retrieves user profile details by ID
/// </summary>
public class GetUserProfileHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDetailDto>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GetUserProfileHandler> _logger;

    public GetUserProfileHandler(ApplicationDbContext context, ILogger<GetUserProfileHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<UserProfileDetailDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Use AsNoTracking() for read-only query optimization
            var userProfile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.UserId && p.IsActive, cancellationToken);

            if (userProfile == null)
            {
                _logger.LogWarning("User profile {UserId} not found or inactive", request.UserId);
                return Result<UserProfileDetailDto>.Failure($"User profile {request.UserId} not found");
            }

            var profileDto = new UserProfileDetailDto
            {
                Id = userProfile.Id,
                Name = userProfile.Name,
                Email = userProfile.Email,
                Bio = userProfile.Bio,
                Age = userProfile.Age,
                Gender = userProfile.Gender,
                Preferences = userProfile.Preferences,
                SexualOrientation = userProfile.SexualOrientation,
                City = userProfile.City,
                State = userProfile.State,
                Country = userProfile.Country,
                PhotoUrls = userProfile.PhotoUrlList,
                PrimaryPhotoUrl = userProfile.PrimaryPhotoUrl,
                Occupation = userProfile.Occupation,
                Company = userProfile.Company,
                Education = userProfile.Education,
                School = userProfile.School,
                Height = userProfile.Height,
                Religion = userProfile.Religion,
                Ethnicity = userProfile.Ethnicity,
                SmokingStatus = userProfile.SmokingStatus,
                DrinkingStatus = userProfile.DrinkingStatus,
                WantsChildren = userProfile.WantsChildren,
                HasChildren = userProfile.HasChildren,
                RelationshipType = userProfile.RelationshipType,
                Interests = userProfile.InterestsList,
                Languages = JsonSerializer.Deserialize<List<string>>(userProfile.Languages ?? "[]") ?? new List<string>(),
                HobbyList = userProfile.HobbyList,
                InstagramHandle = userProfile.InstagramHandle,
                SpotifyTopArtists = userProfile.SpotifyTopArtists,
                IsVerified = userProfile.IsVerified,
                IsPhoneVerified = userProfile.IsPhoneVerified,
                IsEmailVerified = userProfile.IsEmailVerified,
                IsPhotoVerified = userProfile.IsPhotoVerified,
                IsPremium = userProfile.IsPremium,
                SubscriptionType = userProfile.SubscriptionType,
                CreatedAt = userProfile.CreatedAt,
                LastActiveAt = userProfile.LastActiveAt,
                IsOnline = userProfile.IsOnline,
                OnboardingStatus = userProfile.OnboardingStatus,
                OnboardingCompletedAt = userProfile.OnboardingCompletedAt
            };

            return Result<UserProfileDetailDto>.Success(profileDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile {UserId}", request.UserId);
            return Result<UserProfileDetailDto>.Failure(ex);
        }
    }
}
