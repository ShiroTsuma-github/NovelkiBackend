namespace Application.Common.DTOs.Admin;

public sealed record AdminUserDto(
    Guid Id,
    string? Username,
    string? Email,
    DateTimeOffset CreatedAt,
    int BooksCount,
    int TagsCount,
    int AuthorsCreatedCount);

public sealed record AdminAccountDeleteResult(
    Guid UserId,
    int DeletedBooks,
    int DeletedAuthors,
    int DeletedTags);
