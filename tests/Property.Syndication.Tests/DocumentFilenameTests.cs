using ICSharpCode.SharpZipLib.Zip;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Property.Microservice.Services.Implementation;
using Xunit;

namespace Property.Syndication.Tests;

public sealed class DocumentFilenameTests
{
    [Fact]
    public async Task ProtectedPdfRetainsFilenameAndRequiresPassword()
    {
        using var document = new PdfDocument();
        document.AddPage();
        using var source = new MemoryStream();
        document.Save(source, false);
        source.Position = 0;
        var result = await new DocumentEncryptionService().EncryptAsync(source, "Signed lease.pdf", "file-secret", true);
        Assert.Equal("Signed lease.pdf", result.FileName);
        using var protectedStream = new MemoryStream(result.Content);
        using var reopened = PdfReader.Open(protectedStream, "file-secret", PdfDocumentOpenMode.Import);
        Assert.Equal(1, reopened.PageCount);
    }

    [Fact]
    public async Task ProtectedArchivePreservesFullOriginalFilenameAndContents()
    {
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Lease contents"));
        var result = await new DocumentEncryptionService().EncryptAsync(source, "Signed lease.docx", "file-secret", true);
        Assert.Equal("Signed lease.docx.zip", result.FileName);
        using var archive = new ZipFile(new MemoryStream(result.Content));
        archive.Password = "file-secret";
        Assert.Equal("Signed lease.docx", archive[0].Name);
        using var reader = new StreamReader(archive.GetInputStream(archive[0]));
        Assert.Equal("Lease contents", await reader.ReadToEndAsync());
    }
}
