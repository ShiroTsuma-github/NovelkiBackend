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
        ["total"] = BookSearchNumberField.TotalChapters,
        ["totalChapters"] = BookSearchNumberField.TotalChapters
    };

    public static BookSearchCriteria Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BookSearchCriteria.Empty;
        }

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

        filter = new BookSearchFieldFilter(field, value);
        return true;
    }

    private static bool TryParseNumberFilter(string token, out BookSearchNumberFilter filter)
    {
        filter = default!;
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
        var inQuotes = false;

        foreach (var c in query)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
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
}
