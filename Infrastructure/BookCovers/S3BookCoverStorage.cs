namespace Infrastructure.BookCovers;

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

public sealed class S3BookCoverStorage : IBookCoverStorage
{
    private readonly IAmazonS3 _s3;
    private readonly BookCoverOptions _options;
    private readonly string _bucket;

    public S3BookCoverStorage(IAmazonS3 s3, IOptions<BookCoverOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
        _bucket = _options.S3?.Bucket ?? throw new InvalidOperationException("BookCovers:S3:Bucket is not configured.");
    }

    public async Task<BookCoverStoredFile> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
    {
        var validated = await BookCoverStorageValidation.ReadAndValidateAsync(content, contentType, _options.MaxBytes, cancellationToken);
        var storagePath = $"{ownerId:N}/{bookId:N}{validated.Extension}";

        using var uploadStream = new MemoryStream(validated.Bytes, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = storagePath,
            InputStream = uploadStream,
            AutoCloseStream = false,
            ContentType = validated.MimeType,
            UseChunkEncoding = false
        };

        await _s3.PutObjectAsync(request, cancellationToken);
        return new BookCoverStoredFile(storagePath, validated.MimeType, validated.Bytes.Length, null, null);
    }

    public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _s3.GetObjectAsync(_bucket, storagePath, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            throw new EntityNotFoundException<BookCover, Guid>(Guid.Empty);
        }
    }

    public async Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        try
        {
            await _s3.DeleteObjectAsync(_bucket, storagePath, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
        }
    }
}
