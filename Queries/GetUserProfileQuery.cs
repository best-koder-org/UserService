using MediatR;
using UserService.Common;
using UserService.DTOs;

namespace UserService.Queries;

/// <summary>
/// Query to retrieve a user profile by ID
/// Follows CQRS pattern - represents read operation
/// </summary>
public class GetUserProfileQuery : IRequest<Result<UserProfileDetailDto>>
{
    public int UserId { get; set; }

    public GetUserProfileQuery(int userId)
    {
        UserId = userId;
    }
}
