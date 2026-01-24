using FluentValidation;
using UserService.Commands;

namespace UserService.Validators;

/// <summary>
/// Validates CreateUserProfileCommand inputs
/// Separates validation logic from business logic
/// </summary>
public class CreateUserProfileValidator : AbstractValidator<CreateUserProfileCommand>
{
    public CreateUserProfileValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Bio must not exceed 1000 characters");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required")
            .Must(g => new[] { "male", "female", "non-binary", "other" }.Contains(g.ToLower()))
            .WithMessage("Gender must be one of: male, female, non-binary, other");

        RuleFor(x => x.Preferences)
            .NotEmpty().WithMessage("Preferences are required");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required")
            .LessThan(DateTime.Today.AddYears(-18)).WithMessage("Must be at least 18 years old")
            .GreaterThan(DateTime.Today.AddYears(-120)).WithMessage("Invalid date of birth");

        RuleFor(x => x.Height)
            .GreaterThan(0).WithMessage("Height must be greater than 0")
            .LessThan(300).WithMessage("Height must be less than 300 cm")
            .When(x => x.Height > 0);

        RuleFor(x => x.Interests)
            .NotNull().WithMessage("Interests list cannot be null");

        RuleFor(x => x.Languages)
            .NotNull().WithMessage("Languages list cannot be null");

        RuleFor(x => x.SmokingStatus)
            .Must(s => string.IsNullOrEmpty(s) || new[] { "never", "occasionally", "regularly", "trying to quit" }.Contains(s.ToLower()))
            .WithMessage("Invalid smoking status")
            .When(x => !string.IsNullOrEmpty(x.SmokingStatus));

        RuleFor(x => x.DrinkingStatus)
            .Must(d => string.IsNullOrEmpty(d) || new[] { "never", "socially", "regularly", "rarely" }.Contains(d.ToLower()))
            .WithMessage("Invalid drinking status")
            .When(x => !string.IsNullOrEmpty(x.DrinkingStatus));
    }
}
