using Xunit;
using FluentValidation.TestHelper;
using UserService.Commands;
using UserService.Validators;

namespace UserService.Tests.Validators;

public class CreateUserProfileValidatorTests
{
    private readonly CreateUserProfileValidator _validator = new();

    private CreateUserProfileCommand ValidCommand() => new()
    {
        Name = "Jane Doe",
        Email = "jane@example.com",
        Bio = "Test bio",
        Gender = "female",
        Preferences = "male",
        DateOfBirth = new DateTime(1995, 6, 15),
        Interests = new List<string>(),
        Languages = new List<string>()
    };

    // Name
    [Fact]
    public void Name_Empty_HasError()
    {
        var cmd = ValidCommand();
        cmd.Name = "";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_TooLong_HasError()
    {
        var cmd = ValidCommand();
        cmd.Name = new string('a', 101);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Valid_NoError()
    {
        var cmd = ValidCommand();
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    // Email
    [Fact]
    public void Email_Empty_HasError()
    {
        var cmd = ValidCommand();
        cmd.Email = "";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_InvalidFormat_HasError()
    {
        var cmd = ValidCommand();
        cmd.Email = "not-an-email";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
    }

    // Gender
    [Fact]
    public void Gender_Empty_HasError()
    {
        var cmd = ValidCommand();
        cmd.Gender = "";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Gender);
    }

    [Fact]
    public void Gender_InvalidValue_HasError()
    {
        var cmd = ValidCommand();
        cmd.Gender = "attack-helicopter";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Gender);
    }

    [Theory]
    [InlineData("male")]
    [InlineData("female")]
    [InlineData("non-binary")]
    [InlineData("other")]
    public void Gender_ValidValues_NoError(string gender)
    {
        var cmd = ValidCommand();
        cmd.Gender = gender;
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Gender);
    }

    // DateOfBirth
    [Fact]
    public void DateOfBirth_Under18_HasError()
    {
        var cmd = ValidCommand();
        cmd.DateOfBirth = DateTime.Today.AddYears(-17);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void DateOfBirth_Over120_HasError()
    {
        var cmd = ValidCommand();
        cmd.DateOfBirth = DateTime.Today.AddYears(-121);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }

    // Bio
    [Fact]
    public void Bio_TooLong_HasError()
    {
        var cmd = ValidCommand();
        cmd.Bio = new string('x', 1001);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Bio);
    }

    // Height
    [Fact]
    public void Height_Zero_NoError()
    {
        var cmd = ValidCommand();
        cmd.Height = 0;
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Height);
    }

    [Fact]
    public void Height_TooTall_HasError()
    {
        var cmd = ValidCommand();
        cmd.Height = 301;
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Height);
    }

    // SmokingStatus
    [Fact]
    public void SmokingStatus_Invalid_HasError()
    {
        var cmd = ValidCommand();
        cmd.SmokingStatus = "chain-smoker";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SmokingStatus);
    }

    [Theory]
    [InlineData("never")]
    [InlineData("occasionally")]
    [InlineData("regularly")]
    [InlineData("trying to quit")]
    public void SmokingStatus_ValidValues_NoError(string status)
    {
        var cmd = ValidCommand();
        cmd.SmokingStatus = status;
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.SmokingStatus);
    }

    // DrinkingStatus
    [Fact]
    public void DrinkingStatus_Invalid_HasError()
    {
        var cmd = ValidCommand();
        cmd.DrinkingStatus = "alcoholic";
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DrinkingStatus);
    }

    [Theory]
    [InlineData("never")]
    [InlineData("socially")]
    [InlineData("regularly")]
    [InlineData("rarely")]
    public void DrinkingStatus_ValidValues_NoError(string status)
    {
        var cmd = ValidCommand();
        cmd.DrinkingStatus = status;
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.DrinkingStatus);
    }

    // Full valid command
    [Fact]
    public void ValidCommand_NoErrors()
    {
        var cmd = ValidCommand();
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}
