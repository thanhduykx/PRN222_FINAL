using System.Text.RegularExpressions;

namespace PRN222_FINAL.BLL;

internal static partial class ChatGroundingPolicy
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly HashSet<string> ClaimStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "have", "in", "is", "it", "of", "on", "or", "that", "the", "to", "with",
        "va", "la", "cua", "cho", "co", "duoc", "trong", "voi", "mot", "cac", "nhung", "theo", "nay", "do", "thi", "o"
    };

    public const string GroundedAnswerStatus = "grounded_answer";
    public const string PartialAnswerStatus = "partial_answer";
    public const string InsufficientEvidenceStatus = "insufficient_evidence";
    public const string ClarificationRequiredStatus = "clarification_required";
    public const string SmallTalkStatus = "small_talk";

    public static bool HasValidSourceMarkers(string? answer, int sourceCount)
    {
        if (string.IsNullOrWhiteSpace(answer) || sourceCount <= 0)
        {
            return false;
        }

        var markers = SourceMarkerRegex().Matches(answer);
        if (markers.Count == 0
            || markers.Any(marker => !int.TryParse(marker.Groups[1].Value, out var sourceNumber)
                                     || sourceNumber < 1
                                     || sourceNumber > sourceCount))
        {
            return false;
        }

        return ExtractClaimSegments(answer).All(segment => SourceMarkerRegex().IsMatch(segment));
    }

    public static string RemoveSourceMarkers(string? answer)
    {
        return string.IsNullOrEmpty(answer) ? string.Empty : SourceMarkerRegex().Replace(answer, " ");
    }

    public static bool AreClaimsSupportedByCitedSources(
        string? answer,
        IReadOnlyList<string> sourceTexts)
    {
        if (!HasValidSourceMarkers(answer, sourceTexts.Count))
        {
            return false;
        }

        return ExtractClaimSegments(answer!).All(segment =>
        {
            var sourceNumbers = SourceMarkerRegex()
                .Matches(segment)
                .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
            var citedText = string.Join("\n", sourceNumbers.Select(sourceNumber => sourceTexts[sourceNumber - 1] ?? string.Empty));
            return IsClaimSupported(RemoveSourceMarkers(segment), citedText);
        });
    }

    public static NormalizedSourceMarkers NormalizeSourceMarkers(string answer)
    {
        var sourceAnswer = answer ?? string.Empty;
        var sourceNumbers = SourceMarkerRegex()
            .Matches(sourceAnswer)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .ToList();
        if (sourceNumbers.Count == 0)
        {
            return new NormalizedSourceMarkers(sourceAnswer, Array.Empty<int>());
        }

        var numberMap = sourceNumbers
            .Select((sourceNumber, index) => new { sourceNumber, normalizedNumber = index + 1 })
            .ToDictionary(item => item.sourceNumber, item => item.normalizedNumber);
        var normalizedAnswer = SourceMarkerRegex().Replace(
            sourceAnswer,
            match => $"[{numberMap[int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]}]");
        return new NormalizedSourceMarkers(normalizedAnswer, sourceNumbers);
    }

    public static string ResolveAnswerStatus(
        string answerSource,
        bool needsClarification,
        int citationCount)
    {
        if (needsClarification)
        {
            return ClarificationRequiredStatus;
        }

        if (answerSource.Equals("SmallTalk", StringComparison.OrdinalIgnoreCase))
        {
            return SmallTalkStatus;
        }

        return answerSource.Equals("Rag", StringComparison.OrdinalIgnoreCase) && citationCount > 0
            ? GroundedAnswerStatus
            : InsufficientEvidenceStatus;
    }

    [GeneratedRegex(@"\[(\d+)\]")]
    private static partial Regex SourceMarkerRegex();

    private static IEnumerable<string> ExtractClaimSegments(string answer)
    {
        foreach (var line in answer.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var normalizedLine = line.TrimStart('-', '*', '#', ' ');
            if (normalizedLine.Length == 0 || normalizedLine.EndsWith(':'))
            {
                continue;
            }

            foreach (var sentence in Regex.Split(normalizedLine, @"(?<=[.!?])\s+", RegexOptions.None, RegexTimeout))
            {
                var segment = sentence.Trim();
                if (Regex.Matches(RemoveSourceMarkers(segment), @"[\p{L}\p{N}]+", RegexOptions.None, RegexTimeout).Count >= 3)
                {
                    yield return segment;
                }
            }
        }
    }

    private static bool IsClaimSupported(string claim, string citedText)
    {
        if (string.IsNullOrWhiteSpace(claim) || string.IsNullOrWhiteSpace(citedText))
        {
            return false;
        }

        var normalizedClaim = NormalizeForGrounding(claim);
        var normalizedSource = NormalizeForGrounding(citedText);
        var claimFacts = FactRegex().Matches(normalizedClaim).Select(match => NormalizeFact(match.Value)).Distinct().ToList();
        var sourceFacts = FactRegex().Matches(normalizedSource).Select(match => NormalizeFact(match.Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (claimFacts.Any(fact => !sourceFacts.Contains(fact)))
        {
            return false;
        }

        var claimTerms = TokenRegex().Matches(normalizedClaim)
            .Select(match => match.Value)
            .Where(term => term.Length >= 3 && !ClaimStopWords.Contains(term) && !FactRegex().IsMatch(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (claimTerms.Count == 0)
        {
            return claimFacts.Count > 0;
        }

        var sourceTerms = TokenRegex().Matches(normalizedSource)
            .Select(match => match.Value)
            .Where(term => term.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var supportedTerms = claimTerms.Count(claimTerm => sourceTerms.Any(sourceTerm => TermsOverlap(claimTerm, sourceTerm)));
        var requiredTerms = claimTerms.Count <= 4
            ? Math.Min(2, claimTerms.Count)
            : (int)Math.Ceiling(claimTerms.Count * 0.35);
        return supportedTerms >= requiredTerms;
    }

    private static bool TermsOverlap(string left, string right)
    {
        return left.Equals(right, StringComparison.OrdinalIgnoreCase)
               || (left.Length >= 4 && right.Length >= 4
                   && (left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
                       || right.StartsWith(left, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeForGrounding(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character is '\u0111' or '\u0110' ? 'd' : char.ToLowerInvariant(character));
            }
        }

        return Regex.Replace(builder.ToString().Normalize(System.Text.NormalizationForm.FormC), @"[^\p{L}\p{N}%.,\s]+", " ", RegexOptions.None, RegexTimeout);
    }

    private static string NormalizeFact(string fact) => fact.Replace(',', '.').ToUpperInvariant();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\b(?:[a-z]{2,}\d{2,}|\d+(?:[.,]\d+)?%?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FactRegex();
}

internal sealed record NormalizedSourceMarkers(string Answer, IReadOnlyList<int> OriginalSourceNumbers);
