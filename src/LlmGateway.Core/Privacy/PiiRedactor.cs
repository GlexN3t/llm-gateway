using System.Text.RegularExpressions;

namespace LlmGateway.Core.Privacy;

public sealed record PiiRedactionResult(IReadOnlyList<string> Texts, IReadOnlyDictionary<string, string> Map);

public sealed partial class PiiRedactor
{
    private static readonly Regex EmailPattern = EmailRegex();
    private static readonly Regex IbanPattern = IbanRegex();
    private static readonly Regex CardPattern = CardRegex();
    private static readonly Regex PhonePattern = PhoneRegex();

    public PiiRedactionResult Redact(IReadOnlyList<string> texts)
    {
        var map = new Dictionary<string, string>();
        var counters = new Dictionary<string, int>();
        var redacted = new List<string>(texts.Count);

        foreach (var text in texts)
        {
            redacted.Add(RedactText(text, map, counters));
        }

        return new PiiRedactionResult(redacted, map);
    }

    public string Restore(string text, IReadOnlyDictionary<string, string> map)
    {
        foreach (var (token, original) in map)
        {
            text = text.Replace(token, original, StringComparison.Ordinal);
        }

        return text;
    }

    private static string RedactText(string text, Dictionary<string, string> map, Dictionary<string, int> counters)
    {
        text = IbanPattern.Replace(text, m => IsValidIban(m.Value) ? TokenFor("IBAN", m.Value, map, counters) : m.Value);
        text = CardPattern.Replace(text, m => IsValidCard(m.Value) ? TokenFor("CARD", m.Value, map, counters) : m.Value);
        text = EmailPattern.Replace(text, m => TokenFor("EMAIL", m.Value, map, counters));
        text = PhonePattern.Replace(text, m => IsValidPhone(m.Value) ? TokenFor("PHONE", m.Value, map, counters) : m.Value);
        return text;
    }

    private static string TokenFor(string kind, string value, Dictionary<string, string> map, Dictionary<string, int> counters)
    {
        counters.TryGetValue(kind, out var count);
        count++;
        counters[kind] = count;
        var token = $"[{kind}_{count}]";
        map[token] = value;
        return token;
    }

    private static bool IsValidCard(string candidate)
    {
        var digits = candidate.Where(char.IsAsciiDigit).ToArray();
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubleDigit = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    private static bool IsValidIban(string candidate)
    {
        var iban = candidate.Replace(" ", string.Empty).ToUpperInvariant();
        if (iban.Length is < 15 or > 34)
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                remainder = (remainder * 100 + (c - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return remainder == 1;
    }

    private static bool IsValidPhone(string candidate)
    {
        var digits = candidate.Count(char.IsAsciiDigit);
        if (digits is < 7 or > 15)
        {
            return false;
        }

        return candidate.TrimStart().StartsWith('+') || digits >= 10;
    }

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])[A-Z]{2}\d{2}[A-Z0-9]{11,30}(?![A-Z0-9])")]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"(?<![\w-])\d(?:[ -]?\d){12,18}(?!\w)")]
    private static partial Regex CardRegex();

    [GeneratedRegex(@"(?<![\w+])(?<!\d[ -])\+?\d(?:[ ()\-.]{0,2}\d){6,14}(?!\w)(?![\s\-().]*\d)")]
    private static partial Regex PhoneRegex();
}
