using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Queries;

/// <summary>
/// Handles SearchUserProfilesQuery
/// Performs filtered search with pagination
/// </summary>
public class SearchUserProfilesHandler : IRequestHandler<SearchUserProfilesQuery, Result<SearchResultDto<UserProfileSummaryDto>>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SearchUserProfilesHandler> _logger;

    public SearchUserProfilesHandler(ApplicationDbContext context, ILogger<SearchUserProfilesHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<SearchResultDto<UserProfileSummaryDto>>> Handle(SearchUserProfilesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.UserProfiles.Where(p => p.IsActive);

            // Apply filters
            if (request.MinAge.HasValue)
            {
                var maxBirthDate = DateTime.Today.AddYears(-request.MinAge.Value);
                query = query.Where(p => p.DateOfBirth <= maxBirthDate);
            }

            if (request.MaxAge.HasValue)
            {
                var minBirthDate = DateTime.Today.AddYears(-request.MaxAge.Value - 1);
                query = query.Where(p => p.DateOfBirth >= minBirthDate);
            }

            if (!string.IsNullOrEmpty(request.Gender))
            {
                query = query.Where(p => p.Gender == request.Gender);
            }

            if (!string.IsNullOrEmpty(request.Education))
            {
                query = query.Where(p => p.Education.Contains(request.Education));
            }

            if (!string.IsNullOrEmpty(request.Location))
            {
                query = query.Where(p => p.City.Contains(request.Location) ||
                                       p.State.Contains(request.Location) ||
                                       p.Country.Contains(request.Location));
            }

            if (request.IsVerified.HasValue)
            {
                query = query.Where(p => p.IsVerified == request.IsVerified.Value);
            }

            if (request.IsOnline.HasValue)
            {
                query = query.Where(p => p.IsOnline == request.IsOnline.Value);
            }

            // Apply sorting
            query = request.SortBy.ToLower() switch
            {
                "age" => request.SortOrder == "asc" ?
                    query.OrderBy(p => p.DateOfBirth) :
                    query.OrderByDescending(p => p.DateOfBirth),
                "verified" => request.SortOrder == "asc" ?
                    query.OrderBy(p => p.IsVerified) :
                    query.OrderByDescending(p => p.IsVerified),
                _ => request.SortOrder == "asc" ?
                    query.OrderBy(p => p.LastActiveAt) :
                    query.OrderByDescending(p => p.LastActiveAt)
            };

            var totalCount = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            var profiles = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var results = profiles.Select(p =>
            {
                var bio = p.Bio ?? string.Empty;
                var trimmedBio = bio.Length > 150 ? bio.Substring(0, 150) + "..." : bio;

                return new UserProfileSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    Age = DateTime.Today.Year - p.DateOfBirth.Year -
                          (p.DateOfBirth.Date > DateTime.Today.AddYears(-(DateTime.Today.Year - p.DateOfBirth.Year)) ? 1 : 0),
                    City = p.City ?? string.Empty,
                    PrimaryPhotoUrl = p.PrimaryPhotoUrl ?? string.Empty,
                    Bio = trimmedBio,
                    Occupation = p.Occupation ?? string.Empty,
                    Interests = JsonSerializer.Deserialize<List<string>>(p.Interests ?? "[]") ?? new List<string>(),
                    IsVerified = p.IsVerified,
                    IsOnline = p.IsOnline,
                    LastActiveAt = p.LastActiveAt
                };
            }).ToList();

            var searchResult = new SearchResultDto<UserProfileSummaryDto>
            {
                Results = results,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages,
                HasNext = request.Page < totalPages,
                HasPrevious = request.Page > 1
            };

            return Result<SearchResultDto<UserProfileSummaryDto>>.Success(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching user profiles");
            return Result<SearchResultDto<UserProfileSummaryDto>>.Failure(ex);
        }
    }
}
