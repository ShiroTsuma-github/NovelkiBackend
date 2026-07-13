using Application.Common;
using Domain.Repositories;

namespace Application.UnitTests;

public class BookSearchQueryParserTests
{
    [Fact]
    public void Parse_ShouldReadTermsFieldsQuotedValuesAndNumbers()
    {
        var criteria = BookSearchQueryParser.Parse("martial title:\"Lord of Mysteries\" tag:favorite rating>=8 current<120");

        Assert.Equal(new[] { "martial" }, criteria.Terms);
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Title && f.Values.SequenceEqual(["Lord of Mysteries"]));
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Tag && f.Values.SequenceEqual(["favorite"]));
        Assert.Contains(criteria.Numbers, f => f.Field == BookSearchNumberField.Rating && f.Operator == BookSearchOperator.GreaterThanOrEqual && f.Value == 8);
        Assert.Contains(criteria.Numbers, f => f.Field == BookSearchNumberField.CurrentChapter && f.Operator == BookSearchOperator.LessThan && f.Value == 120);
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
        var criteria = BookSearchQueryParser.Parse("genre:fantasy,\"slice of life\" tag:\"to read soon\",favorite");

        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Genre && f.Values.SequenceEqual(["fantasy", "slice of life"]));
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Tag && f.Values.SequenceEqual(["to read soon", "favorite"]));
    }

    [Fact]
    public void Parse_ShouldTreatRatingColonAsEqualNumberFilter()
    {
        var criteria = BookSearchQueryParser.Parse("rating:8 priority:2");

        Assert.Contains(criteria.Numbers, f => f.Field == BookSearchNumberField.Rating && f.Operator == BookSearchOperator.Equal && f.Value == 8);
        Assert.Contains(criteria.Numbers, f => f.Field == BookSearchNumberField.Priority && f.Operator == BookSearchOperator.Equal && f.Value == 2);
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
}
