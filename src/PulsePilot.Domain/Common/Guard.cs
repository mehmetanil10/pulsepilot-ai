using System.Net.Mail;

namespace PulsePilot.Domain.Common;

internal static class Guard
{
    public static Guid NotEmpty(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"{fieldName} cannot be empty.");
        }

        return value;
    }

    public static string RequiredText(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return RequiredText(value, fieldName, maxLength);
    }

    public static string Email(string? value, string fieldName, int maxLength)
    {
        var email = RequiredText(value, fieldName, maxLength);

        if (!MailAddress.TryCreate(email, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"{fieldName} must be a valid email address.");
        }

        return email;
    }

    public static string? OptionalEmail(string? value, string fieldName, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Email(value, fieldName, maxLength);
    }

    public static TEnum DefinedEnum<TEnum>(TEnum value, string fieldName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            throw new DomainException($"{fieldName} has an unsupported value.");
        }

        return value;
    }

    public static DateTimeOffset UtcTimestamp(DateTimeOffset value, string fieldName)
    {
        if (value == default)
        {
            throw new DomainException($"{fieldName} cannot be the default timestamp.");
        }

        return value.ToUniversalTime();
    }
}
