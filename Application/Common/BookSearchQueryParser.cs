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

    private static readonly Dictionary<string, BookSearchNumberField> NumberAliases =
        new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly Dictionary<string, BookSearchDateField> DateAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createDate"] = BookSearchDateField.Created,
        ["created"] = BookSearchDateField.Created,
        ["createdDate"] = BookSearchDateField.Created,
        ["updateDate"] = BookSearchDateField.LastModified,
        ["updated"] = BookSearchDateField.LastModified,
        ["updatedDate"] = BookSearchDateField.LastModified,
        ["lastModified"] = BookSearchDateField.LastModified
    };

    private static readonly Dictionary<string, BookSearchMissingField> MissingAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rating"] = BookSearchMissingField.Rating,
            ["priority"] = BookSearchMissingField.Priority,
            ["author"] = BookSearchMissingField.Author,
            ["genre"] = BookSearchMissingField.Genre,
            ["tag"] = BookSearchMissingField.Tag,
            ["current"] = BookSearchMissingField.CurrentChapter,
            ["currentChapter"] = BookSearchMissingField.CurrentChapter,
            ["progress"] = BookSearchMissingField.CurrentChapter,
            ["total"] = BookSearchMissingField.TotalChapters,
            ["chapter"] = BookSearchMissingField.TotalChapters,
            ["chapters"] = BookSearchMissingField.TotalChapters,
            ["total-chapters"] = BookSearchMissingField.TotalChapters,
            ["totalChapters"] = BookSearchMissingField.TotalChapters,
            ["cover"] = BookSearchMissingField.Cover,
            ["link"] = BookSearchMissingField.Link,
            ["links"] = BookSearchMissingField.Link
        };

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "dd.MM.yyyy",
        "d.M.yyyy",
        "dd/MM/yyyy",
        "d/M/yyyy"
    ];

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
        var dates = new List<BookSearchDateFilter>();
        var missing = new List<BookSearchMissingFilter>();

        foreach (var token in Tokenize(query))
        {
            if (TryParseMissingFilter(token, out var missingFilter))
            {
                missing.Add(missingFilter);
                continue;
            }

            if (TryParseDateFilter(token, out var dateFilters))
            {
                dates.AddRange(dateFilters);
                continue;
            }

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

        return new BookSearchCriteria(terms, fields, numbers, dates, missing);
    }

    private static bool TryParseMissingFilter(string token, out BookSearchMissingFilter filter)
    {
        filter = default!;
        var separatorIndex = token.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var fieldName = token[..separatorIndex];
        var value = Unquote(token[(separatorIndex + 1)..].Trim());
        if (!value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            !MissingAliases.TryGetValue(fieldName, out var field))
        {
            return false;
        }

        filter = new BookSearchMissingFilter(field);
        return true;
    }

    private static bool TryParseDateFilter(string token, out IReadOnlyCollection<BookSearchDateFilter> filters)
    {
        filters = default!;
        var separatorIndex = token.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var fieldName = token[..separatorIndex];
        var valueText = token[(separatorIndex + 1)..].Trim();
        if (!DateAliases.TryGetValue(fieldName, out var field))
        {
            return false;
        }

        var parsedOperator = BookSearchOperator.Equal;
        var hasOperator = false;
        foreach (var op in new[] { ">=", "<=", ">", "<", "=" })
        {
            if (!valueText.StartsWith(op, StringComparison.Ordinal))
            {
                continue;
            }

            valueText = valueText[op.Length..].Trim();
            parsedOperator = ToOperator(op);
            hasOperator = true;
            break;
        }

        if (!hasOperator || !TryParseDatePeriod(Unquote(valueText), out var period))
        {
            return false;
        }

        filters = ToDateFilters(field, parsedOperator, period);
        return true;
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

    private static BookSearchOperator ToOperator(string op)
    {
        return op switch
        {
            ">" => BookSearchOperator.GreaterThan,
            ">=" => BookSearchOperator.GreaterThanOrEqual,
            "<" => BookSearchOperator.LessThan,
            "<=" => BookSearchOperator.LessThanOrEqual,
            _ => BookSearchOperator.Equal
        };
    }

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

    private static bool TryParseDateOnly(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value,
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static bool TryParseDatePeriod(string value, out DatePeriod period)
    {
        if (TryParseDateOnly(value, out var day))
        {
            period = new DatePeriod(day, day.AddDays(1));
            return true;
        }

        if (TryParseMonthPeriod(value, out var monthPeriod))
        {
            period = monthPeriod;
            return true;
        }

        if (value.Length == 4 &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var year) &&
            year is >= 1 and <= 9998)
        {
            var start = new DateOnly(year, 1, 1);
            period = new DatePeriod(start, start.AddYears(1));
            return true;
        }

        period = default;
        return false;
    }

    private static bool TryParseMonthPeriod(string value, out DatePeriod period)
    {
        period = default;
        var separator = value.Contains('-', StringComparison.Ordinal) ? '-' :
            value.Contains('.', StringComparison.Ordinal) ? '.' :
            value.Contains('/', StringComparison.Ordinal) ? '/' :
            '\0';

        if (separator == '\0')
        {
            return false;
        }

        var parts = value.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var firstIsYear = parts[0].Length == 4;
        var secondIsYear = parts[1].Length == 4;
        if (firstIsYear == secondIsYear)
        {
            return false;
        }

        var yearText = firstIsYear ? parts[0] : parts[1];
        var monthText = firstIsYear ? parts[1] : parts[0];
        if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(monthText, NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            year is < 1 or > 9999 ||
            month is < 1 or > 12)
        {
            return false;
        }

        var start = new DateOnly(year, month, 1);
        period = new DatePeriod(start, start.AddMonths(1));
        return true;
    }

    private static IReadOnlyCollection<BookSearchDateFilter> ToDateFilters(
        BookSearchDateField field,
        BookSearchOperator op,
        DatePeriod period)
    {
        return op switch
        {
            BookSearchOperator.Equal =>
            [
                new BookSearchDateFilter(field, BookSearchOperator.GreaterThanOrEqual, period.Start),
                new BookSearchDateFilter(field, BookSearchOperator.LessThan, period.EndExclusive)
            ],
            BookSearchOperator.GreaterThan =>
                [new BookSearchDateFilter(field, BookSearchOperator.GreaterThanOrEqual, period.EndExclusive)],
            BookSearchOperator.GreaterThanOrEqual =>
                [new BookSearchDateFilter(field, BookSearchOperator.GreaterThanOrEqual, period.Start)],
            BookSearchOperator.LessThan => [new BookSearchDateFilter(field, BookSearchOperator.LessThan, period.Start)],
            BookSearchOperator.LessThanOrEqual =>
                [new BookSearchDateFilter(field, BookSearchOperator.LessThan, period.EndExclusive)],
            _ => [new BookSearchDateFilter(field, op, period.Start)]
        };
    }

    private readonly record struct DatePeriod(DateOnly Start, DateOnly EndExclusive);

    private static string NormalizeAliases(string query)
    {
        return query.Replace("total chapters", "total-chapters", StringComparison.OrdinalIgnoreCase);
    }
}
