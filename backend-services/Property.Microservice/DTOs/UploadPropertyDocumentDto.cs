namespace Property.Microservice.DTOs;

/// <summary>
/// DTO for uploading a verification document into a property's deeds vault.
/// Bound from a multipart/form-data request so the controller can read the
/// uploaded file alongside optional metadata.
/// </summary>
public class UploadPropertyDocumentDto
{
    public IFormFile File { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsPasswordProtected { get; set; }
    public string? DocumentPassword { get; set; }
}