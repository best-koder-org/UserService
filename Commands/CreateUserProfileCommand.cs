using MediatR;
using UserService.Common;
using UserService.DTOs;

namespace UserService.Commands;

/// <summary>
/// Command to create a new user profile
/// Follows CQRS pattern - represents write operation
/// </summary>
public class CreateUserProfileCommand : IRequest<Result<UserProfileDetailDto>>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Preferences { get; set; } = string.Empty;
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
