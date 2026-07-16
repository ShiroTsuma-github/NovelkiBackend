using Application.Common;
using Domain.Repositories;

namespace Application.UnitTests;

public class BookSearchQueryParserTests
{
    [Fact]
    public void Parse_ShouldReadTermsFieldsQuotedValuesAndNumbers()
    {
        var criteria =
            BookSearchQueryParser.Parse("martial title:\"Lord of Mysteries\" tag:favorite rating>=8 current<120");

        Assert.Equal(new[] { "martial" }, criteria.Terms);
        Assert.Contains(criteria.Fields,
            f => f.Field == BookSearchField.Title && f.Values.SequenceEqual(["Lord of Mysteries"]));
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Tag && f.Values.SequenceEqual(["favorite"]));
        Assert.Contains(criteria.Numbers,
            f => f.Field == BookSearchNumberField.Rating && f.Operator == BookSearchOperator.GreaterThanOrEqual &&
                 f.Value == 8);
        Assert.Contains(criteria.Numbers,
            f => f.Field == BookSearchNumberField.CurrentChapter && f.Operator == BookSearchOperator.LessThan &&
                 f.Value == 120);
    }

    [Fact]
    public void Parse_ShouldReadSingleQuotedWildcardFieldValues()
    {
        var criteria = BookSearchQueryParser.Parse("author:'Er Gen' title:'i sha*'");

        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Author && f.Values.SequenceEqual(["Er Gen"]));
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Title && f.Values.SequenceEqual(["i sha*"]));
    }

    [Fact]
    public void Parse_ShouldReadMultipleValuesInSingleFieldFilter()
    {
        var criteria =
            BookSearchQueryParser.Parse("genre:fantasy,\"slice of life\" tag:\"to read soon\",favorite");

        Assert.Contains(criteria.Fields,
            f => f.Field == BookSearchField.Genre && f.Values.SequenceEqual(["fantasy", "slice of life"]));
        Assert.Contains(criteria.Fields,
            f => f.Field == BookSearchField.Tag && f.Values.SequenceEqual(["to read soon", "favorite"]));
    }

    [Fact]
    public void Parse_ShouldTreatRatingColonAsEqualNumberFilter()
    {
        var criteria = BookSearchQueryParser.Parse("rating:8 priority:2");

        Assert.Contains(criteria.Numbers,
            f => f.Field == BookSearchNumberField.Rating && f.Operator == BookSearchOperator.Equal && f.Value == 8);
        Assert.Contains(criteria.Numbers,
            f => f.Field == BookSearchNumberField.Priority && f.Operator == BookSearchOperator.Equal && f.Value == 2);
        Assert.Empty(criteria.Fields);
    }

    [Theory]
    [InlineData("rating:>=8", BookSearchNumberField.Rating, BookSearchOperator.GreaterThanOrEqual, 8)]
    [InlineData("rating:>8", BookSearchNumberField.Rating, BookSearchOperator.GreaterThan, 8)]
    [InlineData("priority:<=3", BookSearchNumberField.Priority, BookSearchOperator.LessThanOrEqual, 3)]
    [InlineData("current:=120", BookSearchNumberField.CurrentChapter, BookSearchOperator.Equal, 120)]
    [InlineData("progress:>=50", BookSearchNumberField.CurrentChapter, BookSearchOperator.GreaterThanOrEqual, 50)]
    [InlineData("chapter:<200", BookSearchNumberField.TotalChapters, BookSearchOperator.LessThan, 200)]
    [InlineData("chapters:300", BookSearchNumberField.TotalChapters, BookSearchOperator.Equal, 300)]
    [InlineData("total:<=400", BookSearchNumberField.TotalChapters, BookSearchOperator.LessThanOrEqual, 400)]
    [InlineData("total-chapters:<=450", BookSearchNumberField.TotalChapters, BookSearchOperator.LessThanOrEqual, 450)]
    [InlineData("total chapters:<=475", BookSearchNumberField.TotalChapters, BookSearchOperator.LessThanOrEqual, 475)]
    [InlineData("totalChapters:>500", BookSearchNumberField.TotalChapters, BookSearchOperator.GreaterThan, 500)]
    public void Parse_ShouldTreatColonNumberFiltersWithOperatorsAsNumberFilters(
        string query,
        BookSearchNumberField expectedField,
        BookSearchOperator expectedOperator,
        decimal expectedValue)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        var filter = Assert.Single(criteria.Numbers);
        Assert.Equal(expectedField, filter.Field);
        Assert.Equal(expectedOperator, filter.Operator);
        Assert.Equal(expectedValue, filter.Value);
        Assert.Empty(criteria.Fields);
        Assert.Empty(criteria.Terms);
    }

    [Fact]
    public void Parse_ShouldReturnEmptyCriteriaForBlankQuery()
    {
        var criteria = BookSearchQueryParser.Parse("   ");

        Assert.Same(BookSearchCriteria.Empty, criteria);
    }

    [Fact]
    public void Parse_ShouldTreatInvalidFiltersAsTerms()
    {
        var criteria = BookSearchQueryParser.Parse("title: rating:bad unknown:value progress:abc");

        Assert.Equal(["title:", "rating:bad", "unknown:value", "progress:abc"], criteria.Terms);
        Assert.Empty(criteria.Fields);
        Assert.Empty(criteria.Numbers);
    }

    [Theory]
    [InlineData("rating:none", BookSearchMissingField.Rating)]
    [InlineData("priority:NONE", BookSearchMissingField.Priority)]
    [InlineData("author:'none'", BookSearchMissingField.Author)]
    [InlineData("genre:none", BookSearchMissingField.Genre)]
    [InlineData("tag:none", BookSearchMissingField.Tag)]
    [InlineData("current:none", BookSearchMissingField.CurrentChapter)]
    [InlineData("currentChapter:none", BookSearchMissingField.CurrentChapter)]
    [InlineData("progress:none", BookSearchMissingField.CurrentChapter)]
    [InlineData("total:none", BookSearchMissingField.TotalChapters)]
    [InlineData("cover:none", BookSearchMissingField.Cover)]
    [InlineData("link:none", BookSearchMissingField.Link)]
    [InlineData("links:none", BookSearchMissingField.Link)]
    public void Parse_ShouldReadMissingValueFilters(string query, BookSearchMissingField expectedField)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        var filter = Assert.Single(criteria.Missing);
        Assert.Equal(expectedField, filter.Field);
        Assert.Empty(criteria.Terms);
        Assert.Empty(criteria.Fields);
        Assert.Empty(criteria.Numbers);
    }

    [Theory]
    [InlineData("createDate:>2026-07-15", BookSearchDateField.Created, BookSearchOperator.GreaterThanOrEqual, 2026, 7,
        16)]
    [InlineData("createDate:>=15.07.2026", BookSearchDateField.Created, BookSearchOperator.GreaterThanOrEqual, 2026, 7,
        15)]
    [InlineData("updateDate:<15/07/2026", BookSearchDateField.LastModified, BookSearchOperator.LessThan, 2026, 7, 15)]
    [InlineData("lastModified:<=5/7/2026", BookSearchDateField.LastModified, BookSearchOperator.LessThan, 2026, 7, 6)]
    public void Parse_ShouldReadDateFiltersWithSupportedDateFormats(
        string query,
        BookSearchDateField expectedField,
        BookSearchOperator expectedOperator,
        int year,
        int month,
        int day)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        var filter = Assert.Single(criteria.Dates);
        Assert.Equal(expectedField, filter.Field);
        Assert.Equal(expectedOperator, filter.Operator);
        Assert.Equal(new DateOnly(year, month, day), filter.Value);
        Assert.Empty(criteria.Terms);
    }

    [Theory]
    [InlineData("createdDate:=5.7.2026", BookSearchDateField.Created, 2026, 7, 5, 2026, 7, 6)]
    [InlineData("created:=2026", BookSearchDateField.Created, 2026, 1, 1, 2027, 1, 1)]
    [InlineData("updateDate:=07.2026", BookSearchDateField.LastModified, 2026, 7, 1, 2026, 8, 1)]
    [InlineData("updated:=2026-07", BookSearchDateField.LastModified, 2026, 7, 1, 2026, 8, 1)]
    public void Parse_ShouldExpandEqualDateFiltersToDateRanges(
        string query,
        BookSearchDateField expectedField,
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        Assert.Collection(
            criteria.Dates,
            start =>
            {
                Assert.Equal(expectedField, start.Field);
                Assert.Equal(BookSearchOperator.GreaterThanOrEqual, start.Operator);
                Assert.Equal(new DateOnly(startYear, startMonth, startDay), start.Value);
            },
            end =>
            {
                Assert.Equal(expectedField, end.Field);
                Assert.Equal(BookSearchOperator.LessThan, end.Operator);
                Assert.Equal(new DateOnly(endYear, endMonth, endDay), end.Value);
            });
        Assert.Empty(criteria.Terms);
    }

    [Theory]
    [InlineData("created:>2026", BookSearchDateField.Created, BookSearchOperator.GreaterThanOrEqual, 2027, 1, 1)]
    [InlineData("updated:<=2026-07", BookSearchDateField.LastModified, BookSearchOperator.LessThan, 2026, 8, 1)]
    public void Parse_ShouldReadPartialDateFiltersWithRangeOperators(
        string query,
        BookSearchDateField expectedField,
        BookSearchOperator expectedOperator,
        int year,
        int month,
        int day)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        var filter = Assert.Single(criteria.Dates);
        Assert.Equal(expectedField, filter.Field);
        Assert.Equal(expectedOperator, filter.Operator);
        Assert.Equal(new DateOnly(year, month, day), filter.Value);
        Assert.Empty(criteria.Terms);
    }

    [Theory]
    [InlineData("createDate:2026-07-15")]
    [InlineData("updateDate:=07-15-2026")]
    [InlineData("unknown:none")]
    public void Parse_ShouldTreatInvalidDateAndMissingFiltersAsTerms(string query)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        Assert.Equal([query], criteria.Terms);
        Assert.Empty(criteria.Dates);
        Assert.Empty(criteria.Missing);
    }

    [Fact]
    public void Parse_ShouldContinueFieldValueListAcrossWhitespaceAfterComma()
    {
        var criteria = BookSearchQueryParser.Parse("tag:favorite, to-read, 'slow burn'");

        var field = Assert.Single(criteria.Fields);
        Assert.Equal(BookSearchField.Tag, field.Field);
        Assert.Equal(["favorite", "to-read", "slow burn"], field.Values);
    }

    [Theory]
    [InlineData("total chapters>=200", BookSearchOperator.GreaterThanOrEqual, 200)]
    [InlineData("total-chapters<200", BookSearchOperator.LessThan, 200)]
    public void Parse_ShouldAcceptTotalChapterAliasesWithoutColon(string query, BookSearchOperator expectedOperator,
        decimal expectedValue)
    {
        var criteria = BookSearchQueryParser.Parse(query);

        var filter = Assert.Single(criteria.Numbers);
        Assert.Equal(BookSearchNumberField.TotalChapters, filter.Field);
        Assert.Equal(expectedOperator, filter.Operator);
        Assert.Equal(expectedValue, filter.Value);
    }
}
