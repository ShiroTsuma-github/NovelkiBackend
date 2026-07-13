namespace Application.Common;

using System.Globalization;
using Domain.Repositories;

public static class BookSearchQueryParser
{
    private static readonly Dictionary<string, BookSearchField> FieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = BookSearchField.Title,
        ["author"] = BookSearchField.Author,
        ["tag"] = BookSearchField.Tag,
        ["genre"] = BookSearchField.Genre,
        ["status"] = BookSearchField.Status,
        ["type"] = BookSearchField.Type,
        ["contentType"] = BookSearchField.Type
    };

    private static readonly Dictionary<string, BookSearchNumberField> NumberAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rating"] = BookSearchNumberField.Rating,
        ["priority"] = BookSearchNumberField.Priority,
        ["current"] = BookSearchNumberField.CurrentChapter,
        ["currentChapter"] = BookSearchNumberField.CurrentChapter,
        ["progress"] = BookSearchNumberField.CurrentChapter,
        ["chapter"] = BookSearchNumberField.TotalChapters,
        ["chapters"] = BookSearchNumberField.TotalChapters,
        ["total"] = BookSearchNumberField.TotalChapters,
        ["total-chapters"] = BookSearchNumberField.TotalChapters,
        ["totalChapters"] = BookSearchNumberField.TotalChapters
    };

    public static BookSearchCriteria Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BookSearchCriteria.Empty;
        }

        query = NormalizeAliases(query);

        var terms = new List<string>();
        var fields = new List<BookSearchFieldFilter>();
        var numbers = new List<BookSearchNumberFilter>();

        foreach (var token in Tokenize(query))
        {
            if (TryParseNumberFilter(token, out var numberFilter))
            {
                numbers.Add(numberFilter);
                continue;
            }

            if (TryParseFieldFilter(token, out var fieldFilter))
            {
                fields.Add(fieldFilter);
                continue;
            }

            terms.Add(token);
        }

        return new BookSearchCriteria(terms, fields, numbers);
    }

    private static bool TryParseFieldFilter(string token, out BookSearchFieldFilter filter)
    {
        filter = default!;
        var separatorIndex = token.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var fieldName = token[..separatorIndex];
        var value = token[(separatorIndex + 1)..].Trim();
        if (!FieldAliases.TryGetValue(fieldName, out var field) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var values = SplitFieldValues(value)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();

        if (values.Length == 0)
        {
            return false;
        }

        filter = new BookSearchFieldFilter(field, values);
        return true;
    }

    private static bool TryParseNumberFilter(string token, out BookSearchNumberFilter filter)
    {
        filter = default!;
        if (TryParseNumberFilterWithColonAlias(token, out filter))
        {
            return true;
        }

        foreach (var op in new[] { ">=", "<=", ">", "<", "=" })
        {
            var opIndex = token.IndexOf(op, StringComparison.Ordinal);
            if (opIndex <= 0 || opIndex == token.Length - op.Length)
            {
                continue;
            }

            var fieldName = token[..opIndex];
            var valueText = token[(opIndex + op.Length)..];
            if (!NumberAliases.TryGetValue(fieldName, out var field) ||
                !decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            filter = new BookSearchNumberFilter(field, ToOperator(op), value);
            return true;
        }

        return false;
    }

    private static bool TryParseNumberFilterWithColonAlias(string token, out BookSearchNumberFilter filter)
    {
        filter = default!;
        var separatorIndex = token.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var fieldName = token[..separatorIndex];
        var valueText = token[(separatorIndex + 1)..].Trim();
        if (!NumberAliases.TryGetValue(fieldName, out var field))
        {
            return false;
        }

        var parsedOperator = BookSearchOperator.Equal;
        foreach (var op in new[] { ">=", "<=", ">", "<", "=" })
        {
            if (!valueText.StartsWith(op, StringComparison.Ordinal))
            {
                continue;
            }

            valueText = valueText[op.Length..].Trim();
            parsedOperator = ToOperator(op);
            break;
        }

        if (!decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        filter = new BookSearchNumberFilter(field, parsedOperator, value);
        return true;
    }

    private static BookSearchOperator ToOperator(string op) => op switch
    {
        ">" => BookSearchOperator.GreaterThan,
        ">=" => BookSearchOperator.GreaterThanOrEqual,
        "<" => BookSearchOperator.LessThan,
        "<=" => BookSearchOperator.LessThanOrEqual,
        _ => BookSearchOperator.Equal
    };

    private static IEnumerable<string> Tokenize(string query)
    {
        var token = new List<char>();
        char? quote = null;

        foreach (var c in query)
        {
            if (c is '"' or '\'')
            {
                if (quote == c)
                {
                    quote = null;
                    continue;
                }

                if (quote == null)
                {
                    quote = c;
                    continue;
                }
            }

            if (char.IsWhiteSpace(c) && quote == null)
            {
                if (ShouldContinueFieldValueList(token))
                {
                    continue;
                }

                if (token.Count > 0)
                {
                    yield return new string(token.ToArray()).Trim();
                    token.Clear();
                }

                continue;
            }

            token.Add(c);
        }

        if (token.Count > 0)
        {
            yield return new string(token.ToArray()).Trim();
        }
    }

    private static bool ShouldContinueFieldValueList(List<char> token)
    {
        if (token.Count == 0 || !token.Contains(':'))
        {
            return false;
        }

        for (var i = token.Count - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(token[i]))
            {
                return token[i] == ',';
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitFieldValues(string value)
    {
        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Unquote)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string NormalizeAliases(string query)
    {
        return query.Replace("total chapters", "total-chapters", StringComparison.OrdinalIgnoreCase);
    }
}
