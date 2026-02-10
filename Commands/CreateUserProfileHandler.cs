using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Commands;

/// <summary>
/// Handles CreateUserProfileCommand
/// Contains business logic for creating user profiles
/// </summary>
public class CreateUserProfileHandler : IRequestHandler<CreateUserProfileCommand, Result<UserProfileDetailDto>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CreateUserProfileHandler> _logger;

    public CreateUserProfileHandler(ApplicationDbContext context, ILogger<CreateUserProfileHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<UserProfileDetailDto>> Handle(CreateUserProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Business Rule: Check if email already exists
            var emailExists = await _context.UserProfiles
                .AnyAsync(p => p.Email == request.Email, cancellationToken);

            if (emailExists)
            {
                return Result<UserProfileDetailDto>.Failure("Email already exists");
            }

            // Business Rule: Validate age (must be 18+)
            var age = DateTime.Today.Year - request.DateOfBirth.Year;
            if (request.DateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
            
            if (age < 18)
            {
                return Result<UserProfileDetailDto>.Failure("Must be 18 or older");
            }

            // Create user profile entity
            var userProfile = new UserProfile
            {
                Name = request.Name,
                Email = request.Email,
                Bio = request.Bio,
                Gender = request.Gender,
                Preferences = request.Preferences,
                DateOfBirth = request.DateOfBirth,
                City = request.City,
                State = request.State,
                Country = request.Country,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Occupation = request.Occupation,
                Education = request.Education,
                Height = request.Height,
                Religion = request.Religion,
                SmokingStatus = request.SmokingStatus,
                DrinkingStatus = request.DrinkingStatus,
                WantsChildren = request.WantsChildren,
                HasChildren = request.HasChildren,
                RelationshipType = request.RelationshipType,
                Interests = JsonSerializer.Serialize(request.Interests),
                Languages = JsonSerializer.Serialize(request.Languages),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                IsActive = true,
                IsOnline = true,
                UserId = request.UserId
            };

            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new user profile with ID {UserId} for email {Email}", 
                userProfile.Id, userProfile.Email);

            // Map to DTO
            var profileDto = new UserProfileDetailDto
            {
                Id = userProfile.Id,
                Name = userProfile.Name,
                Email = userProfile.Email,
                Bio = userProfile.Bio,
                Age = age,
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
                Interests = JsonSerializer.Deserialize<List<string>>(userProfile.Interests ?? "[]") ?? new List<string>(),
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
                IsOnline = userProfile.IsOnline
            };

            return Result<UserProfileDetailDto>.Success(profileDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user profile for email {Email}", request.Email);
            return Result<UserProfileDetailDto>.Failure(ex);
        }
    }
}
