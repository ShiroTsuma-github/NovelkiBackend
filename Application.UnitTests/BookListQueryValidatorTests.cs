namespace Application.UnitTests;

using Application.Features.BookFeatures.Queries.GetBook;
using Application.Features.BookFeatures.Validators;

public sealed class BookListQueryValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    public void Lists_ShouldAcceptSupportedPageSizes(int take)
    {
        Assert.True(new GetAllBooksQueryValidator().Validate(new GetAllBooksQuery(0, take)).IsValid);
        Assert.True(new GetAllAdminBooksQueryValidator().Validate(new GetAllAdminBooksQuery(0, take)).IsValid);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 0)]
    [InlineData(0, 501)]
    public void UserList_ShouldRejectInvalidPagination(int skip, int take)
    {
        var result = new GetAllBooksQueryValidator().Validate(new GetAllBooksQuery(skip, take));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 0)]
    [InlineData(0, 501)]
    public void AdminList_ShouldRejectInvalidPagination(int skip, int take)
    {
        var result = new GetAllAdminBooksQueryValidator().Validate(new GetAllAdminBooksQuery(skip, take));

        Assert.False(result.IsValid);
    }
}
