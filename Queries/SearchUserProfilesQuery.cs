using MediatR;
using UserService.Common;
using UserService.DTOs;

namespace UserService.Queries;

/// <summary>
/// Query to search user profiles with filtering and pagination
/// </summary>
public class SearchUserProfilesQuery : IRequest<Result<SearchResultDto<UserProfileSummaryDto>>>
{
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public string? Gender { get; set; }
    public string? Education { get; set; }
    public string? Location { get; set; }
    public bool? IsVerified { get; set; }
    public bool? IsOnline { get; set; }
    public string SortBy { get; set; } = "lastActive";
    public string SortOrder { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
