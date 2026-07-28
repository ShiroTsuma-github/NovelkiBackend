namespace Application.UnitTests;

using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Features.BookFeatures.Queries.GetBook;
using FluentValidation;

public sealed class BookSearchSuggestionTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Handler_ShouldNormalizeFieldAndClampTake()
    {
        var service = new FakeSuggestionService();
        var handler = new GetBookSearchSuggestionsQueryHandler(service, new FakeUser());

        await handler.Handle(
            new GetBookSearchSuggestionsQuery(" AUTHOR ", "  er  ", 500),
            CancellationToken.None);

        Assert.Equal(OwnerId, service.OwnerId);
        Assert.Equal("author", service.Field);
        Assert.Equal("er", service.Search);
        Assert.Equal(20, service.Take);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("title")]
    public async Task Handler_ShouldRejectUnsupportedField(string field)
    {
        var handler = new GetBookSearchSuggestionsQueryHandler(new FakeSuggestionService(), new FakeUser());

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new GetBookSearchSuggestionsQuery(field), CancellationToken.None));
    }

    private sealed class FakeSuggestionService : IBookSearchSuggestionQueryService
    {
        public Guid OwnerId { get; private set; }
        public string? Field { get; private set; }
        public string? Search { get; private set; }
        public int Take { get; private set; }

        public Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetSuggestionsAsync(
            Guid ownerId,
            string field,
            string? search,
            int take,
            CancellationToken cancellationToken)
        {
            OwnerId = ownerId;
            Field = field;
            Search = search;
            Take = take;
            return Task.FromResult<IReadOnlyCollection<BookSearchSuggestionDto>>([]);
        }
    }

    private sealed class FakeUser : IUser
    {
        public Guid? Id => OwnerId;
        public Guid RequiredId => OwnerId;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
