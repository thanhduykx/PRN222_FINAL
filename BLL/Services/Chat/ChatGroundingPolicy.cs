using System.Text.RegularExpressions;

namespace PRN222_FINAL.BLL;

internal static partial class ChatGroundingPolicy
{
    public const string GroundedAnswerStatus = "grounded_answer";
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

            foreach (var sentence in Regex.Split(normalizedLine, @"(?<=[.!?])\s+"))
            {
                var segment = sentence.Trim();
                if (Regex.Matches(RemoveSourceMarkers(segment), @"[\p{L}\p{N}]+").Count >= 3)
                {
                    yield return segment;
                }
            }
        }
    }
}

internal sealed record NormalizedSourceMarkers(string Answer, IReadOnlyList<int> OriginalSourceNumbers);
