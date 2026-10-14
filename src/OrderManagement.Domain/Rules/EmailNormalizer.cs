namespace OrderManagement.Domain.Rules;

/// <summary>
/// Email policy: trim leading and trailing whitespace, then convert to lowercase with the invariant culture.
/// Internationalized domains are not converted to punycode, and provider-specific rules (such as ignoring dots) are not applied.
/// </summary>
public static class EmailNormalizer
{
    public static string Normalize(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().ToLowerInvariant();
    }
}
