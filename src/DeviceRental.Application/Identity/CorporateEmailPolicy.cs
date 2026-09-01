using System.Globalization;

namespace DeviceRental.Application.Identity;

public sealed record CorporateEmailDecision(bool IsAllowed, string? NormalizedEmail);

public sealed class CorporateEmailPolicy
{
    private readonly HashSet<string> _allowedDomains;

    public CorporateEmailPolicy(IEnumerable<string> allowedDomains)
    {
        ArgumentNullException.ThrowIfNull(allowedDomains);
        _allowedDomains = new HashSet<string>(StringComparer.Ordinal);
        foreach (var domain in allowedDomains)
        {
            _allowedDomains.Add(NormalizeDomain(domain));
        }

        if (_allowedDomains.Count == 0)
        {
            throw new ArgumentException("At least one corporate domain is required.", nameof(allowedDomains));
        }
    }

    public CorporateEmailDecision Evaluate(string? email)
    {
        if (!TryNormalizeEmail(email, out var normalizedEmail, out var normalizedDomain))
        {
            return new CorporateEmailDecision(false, null);
        }

        return new CorporateEmailDecision(
            _allowedDomains.Contains(normalizedDomain),
            normalizedEmail);
    }

    private static bool TryNormalizeEmail(
        string? email,
        out string normalizedEmail,
        out string normalizedDomain)
    {
        normalizedEmail = string.Empty;
        normalizedDomain = string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var separator = trimmed.IndexOf('@');
        if (separator <= 0 || separator != trimmed.LastIndexOf('@') || separator == trimmed.Length - 1)
        {
            return false;
        }

        var localPart = trimmed[..separator];
        if (!IsValidLocalPart(localPart))
        {
            return false;
        }

        try
        {
            normalizedDomain = NormalizeDomain(trimmed[(separator + 1)..]);
        }
        catch (ArgumentException)
        {
            return false;
        }

        normalizedEmail = $"{localPart.ToLowerInvariant()}@{normalizedDomain}";
        return true;
    }

    private static bool IsValidLocalPart(string localPart)
    {
        if (localPart.Length == 0 ||
            localPart.StartsWith(".", StringComparison.Ordinal) ||
            localPart.EndsWith(".", StringComparison.Ordinal) ||
            localPart.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        const string symbols = "!#$%&'*+-/=?^_`{|}~.";
        return localPart.All(character =>
            character <= 0x7f && (char.IsLetterOrDigit(character) || symbols.Contains(character)));
    }

    private static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Corporate domain cannot be empty.", nameof(domain));
        }

        var trimmed = domain.Trim();
        if (trimmed.StartsWith(".", StringComparison.Ordinal) ||
            trimmed.EndsWith(".", StringComparison.Ordinal) ||
            trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.Contains('@', StringComparison.Ordinal) ||
            trimmed.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Corporate domain is malformed.", nameof(domain));
        }

        string ascii;
        try
        {
            ascii = new IdnMapping().GetAscii(trimmed).ToLowerInvariant();
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Corporate domain is malformed.", nameof(domain), exception);
        }

        var labels = ascii.Split('.');
        if (labels.Any(label =>
                label.Length == 0 ||
                label.StartsWith("-", StringComparison.Ordinal) ||
                label.EndsWith("-", StringComparison.Ordinal) ||
                label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
        {
            throw new ArgumentException("Corporate domain is malformed.", nameof(domain));
        }

        return ascii;
    }
}
