namespace Application.Common;

public static class BookSortFields
{
    public const string Title = "title";
    public const string Author = "author";
    public const string Status = "status";
    public const string Type = "type";
    public const string Progress = "progress";
    public const string Chapters = "chapters";
    public const string Rating = "rating";
    public const string Priority = "priority";
    public const string Owner = "owner";
    public const string Created = "created";
    public const string LastModified = "lastModified";

    public const string PrimaryTitleAlias = "primarytitle";
    public const string ContentTypeAlias = "contenttype";
    public const string CurrentChapterAlias = "currentchapter";
    public const string ChapterAlias = "chapter";
    public const string TotalChapterAlias = "totalchapter";
    public const string TotalChaptersAlias = "totalchapters";
    public const string OwnerIdAlias = "ownerid";
    public const string CreatedAtAlias = "createdat";
    public const string UpdatedAlias = "updated";
    public const string UpdatedAtAlias = "updatedat";
    public const string NormalizedLastModified = "lastmodified";
}

public static class SortDirections
{
    public const string Ascending = "asc";
    public const string Descending = "desc";
}
