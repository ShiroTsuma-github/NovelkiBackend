namespace Application.Common;

public static class BookValidationMessages
{
    public const string CurrentChapterCannotExceedTotal =
        "Current chapter cannot be greater than total chapters.";
}

public static class BookCoverValidationMessages
{
    public const string EmptyFile = "Cover file is empty.";
    public const string UnsupportedImageFormat = "Cover file must be a JPEG, PNG, or WebP image.";
    public const string InvalidRemoteUrl = "Image URL must be an absolute HTTP or HTTPS URL.";
    public const string RemoteHostNotAllowed = "Image URL host is not allowed.";
}

public static class BookCsvValidationMessages
{
    public const string EmptyFile = "CSV file is empty.";
}
