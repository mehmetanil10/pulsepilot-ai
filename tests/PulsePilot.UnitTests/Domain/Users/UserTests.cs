using PulsePilot.Domain.Common;
using PulsePilot.Domain.Users;

namespace PulsePilot.UnitTests.Domain.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Create_WithValidValues_NormalizesIdentityAndTimestamps()
    {
        var user = User.Create(
            "  Mehmet.Anil@example.com  ",
            "  Mehmet Anıl  ",
            "  generated-password-hash  ",
            CreatedAt);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Mehmet.Anil@example.com", user.Email);
        Assert.Equal("MEHMET.ANIL@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("Mehmet Anıl", user.DisplayName);
        Assert.Equal("generated-password-hash", user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Equal(CreatedAt.ToUniversalTime(), user.CreatedAt);
        Assert.Equal(CreatedAt.ToUniversalTime(), user.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Display Name <user@example.com>")]
    public void Create_WithInvalidEmail_ThrowsDomainException(string? email)
    {
        Assert.Throws<DomainException>(() =>
            User.Create(email!, "Mehmet", "password-hash", CreatedAt));
    }

    [Fact]
    public void Create_WithDisplayNameOverLimit_ThrowsDomainException()
    {
        var displayName = new string('a', User.MaxDisplayNameLength + 1);

        Assert.Throws<DomainException>(() =>
            User.Create("user@example.com", displayName, "password-hash", CreatedAt));
    }

    [Fact]
    public void UpdateDisplayName_WithValidValue_ChangesNameAndTimestamp()
    {
        var user = CreateUser();
        var updatedAt = CreatedAt.AddMinutes(5);

        user.UpdateDisplayName("  New Name  ", updatedAt);

        Assert.Equal("New Name", user.DisplayName);
        Assert.Equal(updatedAt.ToUniversalTime(), user.UpdatedAt);
    }

    [Fact]
    public void UpdateDisplayName_WithOlderTimestamp_PreservesCurrentState()
    {
        var user = CreateUser();

        Assert.Throws<DomainException>(() =>
            user.UpdateDisplayName("New Name", CreatedAt.AddMinutes(-1)));

        Assert.Equal("Mehmet Anıl", user.DisplayName);
        Assert.Equal(CreatedAt.ToUniversalTime(), user.UpdatedAt);
    }

    [Fact]
    public void DeactivateAndActivate_ChangeActiveState()
    {
        var user = CreateUser();

        user.Deactivate(CreatedAt.AddMinutes(1));
        Assert.False(user.IsActive);

        user.Activate(CreatedAt.AddMinutes(2));
        Assert.True(user.IsActive);
        Assert.Equal(CreatedAt.AddMinutes(2).ToUniversalTime(), user.UpdatedAt);
    }

    private static User CreateUser()
    {
        return User.Create(
            "mehmet.anil@example.com",
            "Mehmet Anıl",
            "password-hash",
            CreatedAt);
    }
}
