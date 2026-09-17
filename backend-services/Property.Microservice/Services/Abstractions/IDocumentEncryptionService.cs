using Property.Microservice.DTOs;

namespace Property.Microservice.Services.Abstractions;

/// <summary>
/// Encrypts uploaded documents while preserving their original content and file identity.
/// </summary>
public interface IDocumentEncryptionService
{
    /// <summary>
    /// Returns the document bytes that should be persisted for an upload.
    /// </summary>
    Task<EncryptedDocumentResult> EncryptAsync(
        Stream sourceStream,
        string originalFileName,
        string? documentPassword,
        bool isPasswordProtected,
        CancellationToken cancellationToken = default);
}
