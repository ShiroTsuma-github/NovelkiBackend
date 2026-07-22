namespace Application.UnitTests;

using System.Text.Json;
using Application.Common.DTOs.Book;

public sealed class BookCoverDtoTests
{
    [Fact]
    public void Summary_ShouldSerializeOnlyFieldsUsedByBookLists()
    {
        var summary = new BookCoverSummaryDto
        {
            Status = "Found",
            Source = "Url",
            ImageUrl = "/api/v1/book/example/cover/file",
            ThumbnailImageUrl = "/api/v1/book/example/cover/thumbnail",
            FailureReason = null,
            LastAttemptAt = DateTimeOffset.Parse("2026-07-22T10:00:00+00:00")
        };

        var json = JsonSerializer.SerializeToElement(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.True(json.TryGetProperty("status", out _));
        Assert.True(json.TryGetProperty("imageUrl", out _));
        Assert.True(json.TryGetProperty("thumbnailImageUrl", out _));
        Assert.False(json.TryGetProperty("id", out _));
        Assert.False(json.TryGetProperty("originalImageUrl", out _));
        Assert.False(json.TryGetProperty("mimeType", out _));
        Assert.False(json.TryGetProperty("sizeBytes", out _));
        Assert.False(json.TryGetProperty("width", out _));
        Assert.False(json.TryGetProperty("height", out _));
    }
}
