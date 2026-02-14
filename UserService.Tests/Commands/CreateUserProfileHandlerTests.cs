using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Commands;
using UserService.Data;

namespace UserService.Tests.Commands;

public class CreateUserProfileHandlerTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private CreateUserProfileCommand ValidCommand() => new()
    {
        Name = "Jane Doe",
        Email = "jane@example.com",
        Bio = "Hello world",
        Gender = "female",
        Preferences = "male",
        DateOfBirth = new DateTime(1995, 6, 15),
        City = "Stockholm",
        State = "Stockholm",
        Country = "Sweden",
        Interests = new List<string> { "hiking", "reading" },
        Languages = new List<string> { "English", "Swedish" },
        UserId = Guid.NewGuid()
    };

    [Fact]
    public async Task Handle_ValidProfile_ReturnsSuccess()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jane Doe", result.Value!.Name);
        Assert.Equal("jane@example.com", result.Value.Email);
    }

    [Fact]
    public async Task Handle_ValidProfile_PersistsToDatabase()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(1, await ctx.UserProfiles.CountAsync());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsFailure()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        var cmd = ValidCommand();
        await handler.Handle(cmd, CancellationToken.None);

        // Second attempt with same email
        var cmd2 = ValidCommand();
        cmd2.Name = "Another User";
        cmd2.UserId = Guid.NewGuid();
        var result = await handler.Handle(cmd2, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Email already exists", result.Error!);
    }

    [Fact]
    public async Task Handle_Under18_ReturnsFailure()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        var cmd = ValidCommand();
        cmd.DateOfBirth = DateTime.Today.AddYears(-17); // 17 years old

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("18 or older", result.Error!);
    }

    [Fact]
    public async Task Handle_Exactly18_ReturnsSuccess()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        var cmd = ValidCommand();
        cmd.DateOfBirth = DateTime.Today.AddYears(-18); // Exactly 18 today

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_SetsActiveAndOnline()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.Value!.IsOnline);
    }

    [Fact]
    public async Task Handle_SerializesInterestsAndLanguages()
    {
        using var ctx = CreateContext();
        var handler = new CreateUserProfileHandler(ctx,
            Mock.Of<ILogger<CreateUserProfileHandler>>());

        var cmd = ValidCommand();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Contains("hiking", result.Value!.Interests);
        Assert.Contains("Swedish", result.Value!.Languages);
    }
}
