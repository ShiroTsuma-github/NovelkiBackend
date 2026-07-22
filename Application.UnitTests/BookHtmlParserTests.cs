namespace Application.UnitTests;

using Common;
using Common.Interfaces;
using Domain.Entities;
using Domain.Repositories;
using FluentValidation;
using Infrastructure.BookMetadata;
using Moq;

public sealed class BookHtmlParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldDetectNovelUpdatesAndExtractSafeDraftMetadata()
    {
        var novelId = Guid.NewGuid();
        var fantasyId = Guid.NewGuid();
        var types = new Mock<ITypeRepository>();
        types.Setup(repository => repository.GetByNameAsync("Novel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentType { Id = novelId, Name = "Novel", Slug = "novel" });
        var genres = new Mock<IGenreRepository>();
        genres.Setup(repository => repository.GetByNameAsync("Fantasy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre { Id = fantasyId, Name = "Fantasy", NormalizedName = "FANTASY" });
        genres.Setup(repository => repository.GetByNameAsync("Unmapped", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);
        var parser = new BookHtmlParser([new NovelUpdatesHtmlResolver()], genres.Object, types.Object);
        const string html = """
                            <!doctype html>
                            <html><head><link rel="canonical" href="https://www.novelupdates.com/series/my-book/"></head>
                            <body>
                              <script>globalThis.shouldNeverRun = true</script>
                              <h1 class="seriestitlenu"> My &amp; Book </h1>
                              <div id="showauthors"><a class="genre">Primary Author</a><a class="genre">Alias Record</a></div>
                              <div id="seriesgenre"><a class="genre">Fantasy</a><a class="genre">Unmapped</a></div>
                              <div id="showtags"><a class="genre">Slow Romance</a></div>
                              <div id="showlang"><a class="genre lang">Chinese</a></div>
                              <div id="editdescription">A safe description.</div>
                              <div id="editassociated">Alternative One<br>Alternative Two<br>My &amp; Book</div>
                              <div class="seriesimg"><img src="/img/cover.jpg" onerror="globalThis.bad = true"></div>
                            </body></html>
                            """;

        var result = await parser.ParseAsync(html, CancellationToken.None);

        Assert.Equal("NovelUpdates", result.Source);
        Assert.Equal("My & Book", result.PrimaryTitle);
        Assert.Equal("Primary Author", result.AuthorName);
        Assert.Equal(novelId, result.ContentType?.Id);
        Assert.Equal(["Alternative One", "Alternative Two"], result.AlternativeTitles);
        Assert.Equal(["Slow Romance", "Chinese"], result.Tags);
        Assert.Equal("A safe description.", result.Description);
        Assert.Equal("https://www.novelupdates.com/series/my-book/", result.CanonicalUrl);
        Assert.Equal("https://www.novelupdates.com/img/cover.jpg", result.CoverUrl);
        Assert.Equal(fantasyId, result.Genres.Single(genre => genre.Name == "Fantasy").Id);
        Assert.Null(result.Genres.Single(genre => genre.Name == "Unmapped").Id);
        Assert.Contains(result.Warnings, warning => warning.Contains("Unmapped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_ShouldRecognizeNovelUpdatesFromDistinctiveMarkupWithoutCanonicalUrl()
    {
        var parser = CreateParser();
        const string html = """
                            <h1 class="seriestitlenu">Detected by markup</h1>
                            <div id="seriesgenre"><a class="genre">Fantasy</a></div>
                            """;

        var result = await parser.ParseAsync(html, CancellationToken.None);

        Assert.Equal("Detected by markup", result.PrimaryTitle);
        Assert.Equal("NovelUpdates", result.Source);
    }

    [Fact]
    public async Task ParseAsync_ShouldRejectUnknownHtml()
    {
        var parser = CreateParser();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            parser.ParseAsync("<html><h1>Unknown source</h1></html>", CancellationToken.None));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseAsync_ShouldExtractRoyalRoadSampleAndClassifyLabels()
    {
        var parser = CreateParserWithKnownGenres("Fantasy", "Romance Subplot");

        var result = await parser.ParseAsync(ReadRepositorySample("royalroad.html"), CancellationToken.None);

        Assert.Equal("RoyalRoad", result.Source);
        Assert.Equal("Son of the Hero King: [A Isekai Harem progression Fantasy]", result.PrimaryTitle);
        Assert.Equal("Hikaru_Genji & Adam_King", result.AuthorName);
        Assert.Contains(result.Genres, genre => genre.Id != null && genre.Name == "Fantasy");
        Assert.Contains("Portal Fantasy / Isekai", result.Tags);
        Assert.Contains("Graphic Violence", result.Tags);
        Assert.Contains("Sexual Content", result.Tags);
        Assert.Equal("https://www.royalroad.com/fiction/78110/son-of-the-hero-king-a-isekai-harem-progression",
            result.CanonicalUrl);
        Assert.StartsWith("https://www.royalroadcdn.com/public/covers-large/", result.CoverUrl);
        Assert.NotNull(result.Description);
        Assert.DoesNotContain(result.Tags, tag => tag.Equals("Fantasy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ShouldExtractScribbleHubSampleWithExplicitGenresAndWarningsAsTags()
    {
        var parser = CreateParserWithKnownGenres("Action", "Fantasy", "Romance");

        var result = await parser.ParseAsync(ReadRepositorySample("scribblehub.html"), CancellationToken.None);

        Assert.Equal("ScribbleHub", result.Source);
        Assert.Equal("The Incubus System", result.PrimaryTitle);
        Assert.Equal("Nanakawaichan", result.AuthorName);
        Assert.Contains(result.Genres, genre => genre.Id != null && genre.Name == "Action");
        Assert.Contains("Incubus", result.Tags);
        Assert.Contains("Sexual Content", result.Tags);
        Assert.Contains("Strong Language", result.Tags);
        Assert.Equal("https://www.scribblehub.com/series/124643/the-incubus-system/", result.CanonicalUrl);
        Assert.StartsWith("https://cdn.scribblehub.com/", result.CoverUrl);
        Assert.NotNull(result.Description);
    }

    [Fact]
    public async Task ParseAsync_ShouldExtractWebNovelSampleDespiteInvalidJsonLd()
    {
        var parser = CreateParserWithKnownGenres("Action", "Romance", "Eastern");

        var result = await parser.ParseAsync(ReadRepositorySample("webnovel.html"), CancellationToken.None);

        Assert.Equal("WebNovel", result.Source);
        Assert.Equal("Rise of the Demon God", result.PrimaryTitle, true);
        Assert.Equal("Demonic_angel", result.AuthorName);
        Assert.Contains(result.Genres, genre => genre.Id != null && genre.Name == "Action");
        Assert.Contains(result.Genres, genre => genre.Id != null && genre.Name == "Eastern");
        Assert.Contains("WEAKTOSTRONG", result.Tags);
        Assert.Contains("BEAST TAMING", result.Tags);
        Assert.Equal("https://www.webnovel.com/book/14869533705896705", result.CanonicalUrl);
        Assert.StartsWith("https://book-pic.webnovel.com/", result.CoverUrl);
        Assert.NotNull(result.Description);
    }

    [Theory]
    [InlineData("https://www.royalroad.com/fiction/123/example")]
    [InlineData("https://www.scribblehub.com/series/123/example/")]
    [InlineData("https://www.webnovel.com/book/123")]
    public async Task ParseAsync_ShouldNotTrustCanonicalUrlWithoutSourceMarkup(string canonicalUrl)
    {
        var parser = CreateParserWithKnownGenres();
        var html =
            $"<html><head><link rel='canonical' href='{canonicalUrl}'></head><body><h1>Unrelated page</h1></body></html>";

        await Assert.ThrowsAsync<ValidationException>(() => parser.ParseAsync(html, CancellationToken.None));
    }

    [Fact]
    public async Task ParseAsync_ShouldResolveProtocolRelativeWebNovelCover()
    {
        var parser = CreateParserWithKnownGenres();
        const string html = """
                            <html><head>
                              <link rel="canonical" href="https://www.webnovel.com/book/123">
                              <meta property="og:image" content="//book-pic.webnovel.com/example.jpg">
                            </head><body>
                              <div class="det-hd"><h1>Example</h1><a class="c_primary" title="Author">Author</a></div>
                              <div class="j_synopsis">Description</div>
                            </body></html>
                            """;

        var result = await parser.ParseAsync(html, CancellationToken.None);

        Assert.Equal("https://book-pic.webnovel.com/example.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task ParseAsync_ShouldMatchGenreCandidateByConservativeEditDistance()
    {
        var parser = CreateParserWithKnownGenres("Slice Of Life");
        const string html = """
                            <html><head><link rel="canonical" href="https://www.webnovel.com/book/123"></head><body>
                              <div class="det-hd"><h1>Example</h1><a class="c_primary" title="Author">Author</a></div>
                              <div class="j_tagWrap"><p class="m-tag"><a># SLICEOFLIFEE</a></p></div>
                            </body></html>
                            """;

        var result = await parser.ParseAsync(html, CancellationToken.None);

        var genre = Assert.Single(result.Genres);
        Assert.NotNull(genre.Id);
        Assert.Equal("Slice Of Life", genre.Name);
        Assert.DoesNotContain("SLICEOFLIFEE", result.Tags);
    }

    [Fact]
    public async Task ParseAsync_ShouldUseExistingTagCasingWhenOnlySpacesDiffer()
    {
        var ownerId = Guid.NewGuid();
        var types = new Mock<ITypeRepository>();
        var genres = new Mock<IGenreRepository>();
        var tags = new Mock<ITagRepository>();
        tags.Setup(repository => repository.GetByNamesAsync(
                ownerId,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Tag
                {
                    OwnerId = ownerId,
                    Name = "Slice Of Life",
                    NormalizedName = MappingExtensions.NormalizeName("Slice Of Life")
                }
            ]);
        var user = new Mock<IUser>();
        user.SetupGet(current => current.Id).Returns(ownerId);
        var parser = new BookHtmlParser(
            [new NovelUpdatesHtmlResolver()],
            genres.Object,
            types.Object,
            tags.Object,
            user.Object);
        const string html = """
                            <html><head><link rel="canonical" href="https://www.novelupdates.com/series/example/"></head>
                              <body><h1 class="seriestitlenu">Example</h1><div id="showtags"><a class="genre">SLICEOFLIFEE</a></div></body></html>
                            """;

        var result = await parser.ParseAsync(html, CancellationToken.None);

        Assert.Equal(["Slice Of Life"], result.Tags);
    }

    private static BookHtmlParser CreateParser()
    {
        var types = new Mock<ITypeRepository>();
        types.Setup(repository => repository.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentType?)null);
        var genres = new Mock<IGenreRepository>();
        genres.Setup(repository => repository.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);
        return new BookHtmlParser([new NovelUpdatesHtmlResolver()], genres.Object, types.Object);
    }

    private static BookHtmlParser CreateParserWithKnownGenres(params string[] names)
    {
        var types = new Mock<ITypeRepository>();
        types.Setup(repository => repository.GetByNameAsync("Novel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentType { Id = Guid.NewGuid(), Name = "Novel", Slug = "novel" });
        var known = names.ToDictionary(MappingExtensions.NormalizeNameIgnoringSpaces,
            name => new Genre { Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant() },
            StringComparer.Ordinal);
        var genres = new Mock<IGenreRepository>();
        genres.Setup(repository => repository.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => known.Values
                .Where(genre => MetadataNameSimilarity.IsPracticalMatch(genre.Name, name))
                .OrderBy(genre => MetadataNameSimilarity.MatchDistance(genre.Name, name))
                .FirstOrDefault());
        return new BookHtmlParser(
            [
                new NovelUpdatesHtmlResolver(), new RoyalRoadHtmlResolver(), new ScribbleHubHtmlResolver(),
                new WebNovelHtmlResolver()
            ],
            genres.Object,
            types.Object);
    }

    private static string ReadRepositorySample(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }
}
