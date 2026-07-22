namespace Application.Common;

using System.Globalization;
using System.Text;

public static class MetadataNameSimilarity
{
    public static string CreateKey(string value)
    {
        var decomposed = MappingExtensions.CollapseWhitespace(value).Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToUpperInvariant(character));
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool IsPracticalMatch(string left, string right)
    {
        var leftKey = CreateKey(left);
        var rightKey = CreateKey(right);
        if (leftKey.Length == 0 || rightKey.Length == 0)
        {
            return false;
        }

        if (string.Equals(leftKey, rightKey, StringComparison.Ordinal))
        {
            return true;
        }

        var minimumLength = Math.Min(leftKey.Length, rightKey.Length);
        var maximumLength = Math.Max(leftKey.Length, rightKey.Length);
        if (minimumLength < 8)
        {
            return false;
        }

        var maximumDistance = maximumLength >= 16 ? 2 : 1;
        if (maximumLength - minimumLength > maximumDistance)
        {
            return false;
        }

        var distance = LevenshteinDistance(leftKey, rightKey, maximumDistance);
        return distance <= maximumDistance && 1d - distance / (double)maximumLength >= 0.9d;
    }

    public static int MatchDistance(string left, string right)
    {
        var leftKey = CreateKey(left);
        var rightKey = CreateKey(right);
        return LevenshteinDistance(leftKey, rightKey, int.MaxValue);
    }

    private static int LevenshteinDistance(string left, string right, int cutoff)
    {
        if (left.Length > right.Length)
        {
            (left, right) = (right, left);
        }

        if (right.Length - left.Length > cutoff)
        {
            return cutoff == int.MaxValue ? right.Length - left.Length : cutoff + 1;
        }

        var previous = new int[left.Length + 1];
        var current = new int[left.Length + 1];
        for (var index = 0; index <= left.Length; index++)
        {
            previous[index] = index;
        }

        for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
        {
            current[0] = rightIndex;
            var rowMinimum = current[0];
            for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
            {
                var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[leftIndex] = Math.Min(
                    Math.Min(current[leftIndex - 1] + 1, previous[leftIndex] + 1),
                    previous[leftIndex - 1] + substitutionCost);
                rowMinimum = Math.Min(rowMinimum, current[leftIndex]);
            }

            if (rowMinimum > cutoff)
            {
                return cutoff + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[left.Length];
    }
}
