using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace e_Commerce_application.Services
{
    public record StoredFile(string Key, FileKind Kind, long Size);

    public static class Uploads
    {
        // Validates an image upload; adds a model error and returns null if it is not acceptable.
        public static async Task<FileKind?> CheckImageAsync(IFormFile? file, string field, ModelStateDictionary modelState)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }
            if (file.Length > FileSignatures.MaxImageBytes)
            {
                modelState.AddModelError(field, "Images must be 5 MB or smaller.");
                return null;
            }
            var kind = await FileSignatures.DetectImageAsync(file);
            if (kind == null)
            {
                modelState.AddModelError(field, "Images must be JPG, PNG or WEBP files.");
            }
            return kind;
        }

        public static async Task<FileKind?> CheckManuscriptAsync(IFormFile? file, string field, ModelStateDictionary modelState)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }
            if (file.Length > FileSignatures.MaxManuscriptBytes)
            {
                modelState.AddModelError(field, "Manuscripts must be 50 MB or smaller.");
                return null;
            }
            var kind = await FileSignatures.DetectManuscriptAsync(file);
            if (kind == null)
            {
                modelState.AddModelError(field, "Manuscripts must be PDF or EPUB files.");
            }
            return kind;
        }

        public static async Task<FileKind?> CheckDigitalAsync(IFormFile? file, string field, ModelStateDictionary modelState)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }
            if (file.Length > FileSignatures.MaxDigitalBytes)
            {
                modelState.AddModelError(field, "Files must be 100 MB or smaller.");
                return null;
            }
            var kind = await FileSignatures.DetectDigitalAsync(file);
            if (kind == null)
            {
                modelState.AddModelError(field, "Unsupported file. Upload a PDF, EPUB, ZIP, MP3, WAV, MP4, JPG, PNG or WEBP file.");
            }
            return kind;
        }

        public static async Task<StoredFile> SaveAsync(this IFileStorage storage, IFormFile file, FileKind kind, string folder)
        {
            await using var stream = file.OpenReadStream();
            var key = await storage.SaveAsync(stream, folder, kind.Extension);
            return new StoredFile(key, kind, file.Length);
        }
    }
}
