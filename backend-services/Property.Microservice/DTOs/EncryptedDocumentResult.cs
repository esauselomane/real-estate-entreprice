namespace Property.Microservice.DTOs;

/// <summary>
/// Describes the bytes and download identity produced for an uploaded document.
/// </summary>
public sealed record EncryptedDocumentResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public required string FileExtension { get; init; }
    public bool IsPasswordProtected { get; init; }
}
