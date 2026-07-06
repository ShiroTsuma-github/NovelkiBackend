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
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Title && f.Value == "Lord of Mysteries");
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Tag && f.Value == "favorite");
        Assert.Contains(criteria.Numbers, f => f.Field == BookSearchNumberField.Rating && f.Operator == BookSearchOperator.GreaterThanOrEqual && f.Value == 8);
        Assert.Contains(criteria.Numbers, f => f.Field == BookSearchNumberField.CurrentChapter && f.Operator == BookSearchOperator.LessThan && f.Value == 120);
    }

    [Fact]
    public void Parse_ShouldReadSingleQuotedWildcardFieldValues()
    {
        var criteria = BookSearchQueryParser.Parse("author:'Er Gen' title:'i sha*'");

        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Author && f.Value == "Er Gen");
        Assert.Contains(criteria.Fields, f => f.Field == BookSearchField.Title && f.Value == "i sha*");
    }
}
