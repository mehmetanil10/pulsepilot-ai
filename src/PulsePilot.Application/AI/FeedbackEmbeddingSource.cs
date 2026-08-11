using System.Security.Cryptography;
using System.Text;

namespace PulsePilot.Application.AI;

public static class FeedbackEmbeddingSource
{
    public static string CreateText(string? title, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return string.IsNullOrWhiteSpace(title)
            ? content.Trim()
            : $"Title: {title.Trim()}\nContent: {content.Trim()}";
    }

    public static string ComputeHash(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
