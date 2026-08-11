using PulsePilot.Application.Authentication;

namespace PulsePilot.UnitTests.Application.Authentication;

public sealed class AuthenticationValidatorTests
{
    [Fact]
    public async Task RegisterCommand_WithValidValues_IsValid()
    {
        var command = new RegisterCommand(
            "owner@example.com",
            "Workspace Owner",
            "correct-horse-battery-staple",
            "Example Workspace");

        var result = await new RegisterCommandValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("invalid-email", "correct-horse-battery-staple", "Workspace")]
    [InlineData("owner@example.com", "too-short", "Workspace")]
    [InlineData("owner@example.com", "correct-horse-battery-staple", "")]
    public async Task RegisterCommand_WithInvalidValues_IsRejected(
        string email,
        string password,
        string workspaceName)
    {
        var command = new RegisterCommand(
            email,
            "Workspace Owner",
            password,
            workspaceName);

        var result = await new RegisterCommandValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("invalid-email", "password")]
    [InlineData("owner@example.com", "")]
    public async Task LoginCommand_WithInvalidValues_IsRejected(string email, string password)
    {
        var result = await new LoginCommandValidator().ValidateAsync(
            new LoginCommand(email, password));

        Assert.False(result.IsValid);
    }
}
