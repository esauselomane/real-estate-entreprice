using ICSharpCode.SharpZipLib.Zip;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Security;
using Property.Microservice.DTOs;
using Property.Microservice.Services.Abstractions;

namespace Property.Microservice.Services.Implementation;

public class DocumentEncryptionService : IDocumentEncryptionService
{
    private const string PdfContentType = "application/pdf";
    private const string ZipContentType = "application/zip";
    private const string PdfExtension = ".pdf";
    private const string ZipExtension = ".zip";

    public async Task<EncryptedDocumentResult> EncryptAsync(
        Stream sourceStream,
        string originalFileName,
        string? documentPassword,
        bool isPasswordProtected,
        CancellationToken cancellationToken = default)
    {
        if (sourceStream == null) throw new ArgumentNullException(nameof(sourceStream));
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentException("A file name is required.", nameof(originalFileName));

        if (!isPasswordProtected)
        {
            return await CopyDocumentAsync(sourceStream, originalFileName, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(documentPassword))
        {
            throw new ArgumentException("A document password is required.", nameof(documentPassword));
        }

        if (IsPdf(originalFileName))
        {
            return await EncryptPdfAsync(sourceStream, originalFileName, documentPassword, cancellationToken);
        }

        return await CreateEncryptedZipAsync(sourceStream, originalFileName, documentPassword, cancellationToken);
    }

    private static async Task<EncryptedDocumentResult> CopyDocumentAsync(
        Stream sourceStream,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream, cancellationToken);
        var extension = Path.GetExtension(originalFileName);

        return new EncryptedDocumentResult
        {
            Content = memoryStream.ToArray(),
            ContentType = GetContentType(originalFileName),
            FileName = Path.GetFileName(originalFileName),
            FileExtension = extension,
            IsPasswordProtected = false
        };
    }

    private static async Task<EncryptedDocumentResult> EncryptPdfAsync(
        Stream sourceStream,
        string originalFileName,
        string documentPassword,
        CancellationToken cancellationToken)
    {
        await using var sourceMemoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(sourceMemoryStream, cancellationToken);
        sourceMemoryStream.Position = 0;

        using var outputMemoryStream = new MemoryStream();
        using (var sourceDocument = PdfReader.Open(sourceMemoryStream, PdfDocumentOpenMode.Import))
        using (var protectedDocument = new PdfDocument())
        {
            foreach (var sourcePage in sourceDocument.Pages)
            {
                protectedDocument.AddPage(sourcePage);
            }

            protectedDocument.SecuritySettings.UserPassword = documentPassword;
            protectedDocument.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            protectedDocument.Save(outputMemoryStream);
        }

        return new EncryptedDocumentResult
        {
            Content = outputMemoryStream.ToArray(),
            ContentType = PdfContentType,
            FileName = Path.GetFileName(originalFileName),
            FileExtension = PdfExtension,
            IsPasswordProtected = true
        };
    }

    private static async Task<EncryptedDocumentResult> CreateEncryptedZipAsync(
        Stream sourceStream,
        string originalFileName,
        string documentPassword,
        CancellationToken cancellationToken)
    {
        await using var outputMemoryStream = new MemoryStream();
        using var zipOutputStream = new ZipOutputStream(outputMemoryStream);
        zipOutputStream.Password = documentPassword;
        zipOutputStream.SetLevel(9);

        var entryName = Path.GetFileName(originalFileName);
        var zipEntry = new ZipEntry(entryName)
        {
            DateTime = DateTime.UtcNow,
            AESKeySize = 256
        };

        zipOutputStream.PutNextEntry(zipEntry);
        await sourceStream.CopyToAsync(zipOutputStream, cancellationToken);
        zipOutputStream.CloseEntry();
        zipOutputStream.Finish();

        var protectedFileName = GetProtectedFileName(originalFileName);

        return new EncryptedDocumentResult
        {
            Content = outputMemoryStream.ToArray(),
            ContentType = ZipContentType,
            FileName = protectedFileName,
            FileExtension = ZipExtension,
            IsPasswordProtected = true
        };
    }

    private static bool IsPdf(string originalFileName)
    {
        return Path.GetExtension(originalFileName).Equals(PdfExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContentType(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => PdfContentType,
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }

    private static string GetProtectedFileName(string originalFileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        return string.IsNullOrWhiteSpace(fileNameWithoutExtension)
            ? $"document{ZipExtension}"
            : $"{fileNameWithoutExtension}{ZipExtension}";
    }
}
