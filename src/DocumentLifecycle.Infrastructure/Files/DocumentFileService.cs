using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Files;
using DocumentLifecycle.Domain.Activity;
using DocumentLifecycle.Domain.Common;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocumentLifecycle.Infrastructure.Files;

internal sealed class DocumentFileService(
    ApplicationDbContext database,
    WorkspaceUploadPathResolver pathResolver,
    IOptions<FileStorageOptions> options,
    IClock clock) : IDocumentFileService
{
    private static readonly IReadOnlyDictionary<string, string> AllowedMediaTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
        };

    public async Task<DocumentFileMutationResult> UploadRevisionAsync(
        Guid documentPublicId,
        RevisionUploadInput upload,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var document = await database.ManagedDocuments
            .Include(item => item.Revisions)
            .SingleOrDefaultAsync(item => item.PublicId == documentPublicId, cancellationToken);
        if (document is null)
        {
            return DocumentFileMutationResult.Missing();
        }

        if (document.State == LifecycleState.Archived)
        {
            return DocumentFileMutationResult.Reject("Archived documents cannot receive a revision.");
        }

        var changeNote = upload.ChangeNote.Trim();
        if (changeNote.Length is 0 or > 500)
        {
            return DocumentFileMutationResult.Reject("A change note of 1 to 500 characters is required.");
        }

        var originalFilename = SafeFileName.Sanitize(upload.OriginalFilename);
        var extension = Path.GetExtension(originalFilename).ToLowerInvariant();
        if (!AllowedMediaTypes.TryGetValue(extension, out var expectedMediaType))
        {
            return DocumentFileMutationResult.Reject("Upload a PDF, PNG, JPG, or JPEG file.");
        }

        if (!string.Equals(upload.DeclaredMediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return DocumentFileMutationResult.Reject("The file extension and media type do not match.");
        }

        var maximumSize = options.Value.MaximumFileSizeBytes;
        if (upload.DeclaredLength is < 1 || upload.DeclaredLength > maximumSize)
        {
            return DocumentFileMutationResult.Reject("The file must contain data and cannot exceed 10 MB.");
        }

        var workspaceDirectory = pathResolver.GetWorkspaceDirectory(document.WorkspaceId);
        Directory.CreateDirectory(workspaceDirectory);
        var storedFilename = $"{Guid.NewGuid():N}{CanonicalExtension(extension)}";
        var finalPath = pathResolver.GetFilePath(document.WorkspaceId, storedFilename);
        var temporaryFilename = $"{Guid.NewGuid():N}.uploading";
        var temporaryPath = pathResolver.GetFilePath(document.WorkspaceId, temporaryFilename);

        try
        {
            var storedFile = await CopyAndHashAsync(
                upload.Content,
                temporaryPath,
                maximumSize,
                cancellationToken);
            if (storedFile is null)
            {
                return DocumentFileMutationResult.Reject("The file must contain data and cannot exceed 10 MB.");
            }

            if (!await HasValidSignatureAsync(
                    temporaryPath,
                    expectedMediaType,
                    storedFile.Value.Length,
                    cancellationToken))
            {
                return DocumentFileMutationResult.Reject("The file signature does not match its declared type.");
            }

            File.Move(temporaryPath, finalPath);
            var now = clock.UtcNow;
            try
            {
                var revision = document.AddRevision(
                    changeNote,
                    originalFilename,
                    storedFilename,
                    expectedMediaType,
                    storedFile.Value.Length,
                    storedFile.Value.Sha256Hash,
                    actor,
                    now);
                database.AuditEvents.Add(AuditEvent.Create(
                    document.WorkspaceId,
                    actor,
                    "RevisionUploaded",
                    nameof(ManagedDocument),
                    document.PublicId,
                    now,
                    JsonSerializer.Serialize(new
                    {
                        document.Code,
                        Revision = revision.RevisionNumber,
                        Filename = originalFilename,
                        Size = storedFile.Value.Length,
                    })));
                await database.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                DeleteIfExists(finalPath);
                throw;
            }

            return DocumentFileMutationResult.Success($"Revision uploaded to {document.Code}.");
        }
        catch (DomainRuleException exception)
        {
            return DocumentFileMutationResult.Reject(exception.Message);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
        }
    }

    public async Task<DocumentDownload?> GetDownloadAsync(
        Guid documentPublicId,
        Guid revisionPublicId,
        bool allowDraft,
        CancellationToken cancellationToken = default)
    {
        var document = await database.ManagedDocuments
            .AsNoTracking()
            .Include(item => item.Revisions)
            .SingleOrDefaultAsync(item => item.PublicId == documentPublicId, cancellationToken);
        if (document is null || (!allowDraft && document.State == LifecycleState.Draft))
        {
            return null;
        }

        var revision = document.Revisions.SingleOrDefault(item => item.PublicId == revisionPublicId);
        if (revision is null)
        {
            return null;
        }

        var filePath = pathResolver.GetFilePath(document.WorkspaceId, revision.StoredFilename);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return new DocumentDownload(
            stream,
            revision.MediaType,
            revision.OriginalFilename,
            revision.Size,
            revision.Sha256Hash);
    }

    private static async Task<StoredFile?> CopyAndHashAsync(
        Stream input,
        string destinationPath,
        long maximumSize,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                length += read;
                if (length > maximumSize)
                {
                    return null;
                }

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
            return length == 0
                ? null
                : new StoredFile(length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<bool> HasValidSignatureAsync(
        string path,
        string mediaType,
        long length,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[8];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        var prefixLength = await stream.ReadAsync(prefix, cancellationToken);

        if (mediaType == "application/pdf")
        {
            return prefixLength >= 5 && prefix.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        }

        if (mediaType == "image/png")
        {
            return prefixLength == 8 && prefix.AsSpan().SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        }

        if (mediaType == "image/jpeg" && prefixLength >= 3 &&
            prefix[0] == 0xff && prefix[1] == 0xd8 && prefix[2] == 0xff && length >= 4)
        {
            stream.Seek(-2, SeekOrigin.End);
            var suffix = new byte[2];
            return await stream.ReadAsync(suffix, cancellationToken) == 2 &&
                suffix[0] == 0xff && suffix[1] == 0xd9;
        }

        return false;
    }

    private static string CanonicalExtension(string extension) =>
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : extension;

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private readonly record struct StoredFile(long Length, string Sha256Hash);
}
