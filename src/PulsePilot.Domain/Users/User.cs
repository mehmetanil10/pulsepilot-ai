using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Users;

public sealed class User : AuditableEntity
{
    public const int MaxEmailLength = 320;
    public const int MaxDisplayNameLength = 120;
    public const int MaxPasswordHashLength = 2_048;

    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        Email = Guard.Email(email, nameof(email), MaxEmailLength);
        NormalizedEmail = Email.ToUpperInvariant();
        DisplayName = Guard.RequiredText(displayName, nameof(displayName), MaxDisplayNameLength);
        PasswordHash = Guard.RequiredText(passwordHash, nameof(passwordHash), MaxPasswordHashLength);
        IsActive = true;
    }

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public static User Create(
        string email,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        return new User(Guid.CreateVersion7(), email, displayName, passwordHash, createdAt);
    }

    public void UpdateDisplayName(string displayName, DateTimeOffset updatedAt)
    {
        var validatedDisplayName = Guard.RequiredText(
            displayName,
            nameof(displayName),
            MaxDisplayNameLength);

        MarkUpdated(updatedAt);
        DisplayName = validatedDisplayName;
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset updatedAt)
    {
        var validatedPasswordHash = Guard.RequiredText(
            passwordHash,
            nameof(passwordHash),
            MaxPasswordHashLength);

        MarkUpdated(updatedAt);
        PasswordHash = validatedPasswordHash;
    }

    public void Deactivate(DateTimeOffset updatedAt)
    {
        if (!IsActive)
        {
            return;
        }

        MarkUpdated(updatedAt);
        IsActive = false;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        if (IsActive)
        {
            return;
        }

        MarkUpdated(updatedAt);
        IsActive = true;
    }
}
