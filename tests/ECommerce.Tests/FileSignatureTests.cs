using e_Commerce_application.Services;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Tests
{
    public class FileSignatureTests
    {
        private static IFormFile File(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

        [Fact]
        public async Task Detects_ManuscriptTypes_ByContent()
        {
            Assert.Equal(FileSignatures.Pdf, await FileSignatures.DetectManuscriptAsync(File(SamplePdf.Create("t", "a", Array.Empty<string>()), "x.pdf")));

            var epub = new byte[64];
            "PK\u0003\u0004"u8.CopyTo(epub);
            "mimetypeapplication/epub+zip"u8.CopyTo(epub.AsSpan(30));
            Assert.Equal(FileSignatures.Epub, await FileSignatures.DetectManuscriptAsync(File(epub, "x.epub")));

            Assert.Null(await FileSignatures.DetectManuscriptAsync(File("<svg></svg>"u8.ToArray(), "evil.pdf")));
        }

        [Fact]
        public void Detects_DigitalDownloadTypes()
        {
            Assert.Equal(FileSignatures.Zip, FileSignatures.Detect(SampleFiles.Zip(("a.txt", "hi"))));
            Assert.Equal(FileSignatures.Wav, FileSignatures.Detect(SampleFiles.Tone(440, 0.1)[..64]));
            Assert.Equal(FileSignatures.Mp3, FileSignatures.Detect("ID3\u0004\0\0\0\0\0\0"u8.ToArray()));
            Assert.Equal(FileSignatures.Mp4, FileSignatures.Detect(new byte[] { 0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' }));
            Assert.Null(FileSignatures.Detect("#!/bin/sh\nrm -rf /"u8.ToArray()));
            Assert.Null(FileSignatures.Detect("MZ\u0090\0"u8.ToArray()));
        }

        [Fact]
        public async Task Detects_ImageTypes_AndRejectsSvg()
        {
            Assert.Equal(FileSignatures.Png, await FileSignatures.DetectImageAsync(File(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "a.png")));
            Assert.Equal(FileSignatures.Jpeg, await FileSignatures.DetectImageAsync(File(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "a.jpg")));
            Assert.Null(await FileSignatures.DetectImageAsync(File("<svg onload=alert(1)>"u8.ToArray(), "a.png")));
        }
    }
}
