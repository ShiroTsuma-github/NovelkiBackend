namespace Application.Common.Models;

public sealed record BookCoverFileResult(Stream Content, string MimeType, string FileName);
