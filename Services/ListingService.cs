using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace e_Commerce_application.Services
{
    // Validation and persistence for the create/edit listing form.
    public class ListingService
    {
        private readonly IFileStorage _storage;

        public ListingService(IFileStorage storage) => _storage = storage;

        public static ListingFormViewModel ToForm(Product p) => new()
        {
            ProductCode = p.ProductCode,
            Type = p.Type,
            Name = p.Name,
            Subtitle = p.Subtitle,
            AuthorName = p.AuthorName,
            Description = p.Description ?? string.Empty,
            Category = p.Category,
            Language = p.Language,
            PageCount = p.PageCount,
            Price = p.Price,
            ListPrice = p.ListPrice,
            Stock = p.Stock,
            DurationMinutes = p.DurationMinutes,
            ServiceLocation = p.ServiceLocation,
            ServiceArea = p.ServiceArea,
            ExistingFileName = p.FileName,
            HasImage = p.ImagePath != null,
            Status = p.Status,
            BlockedReason = p.BlockedReason
        };

        // Restores the read-only parts of the form after a failed post.
        public static void Refill(ListingFormViewModel model, Product product)
        {
            model.ProductCode = product.ProductCode;
            model.Type = product.Type;
            model.ExistingFileName = product.FileName;
            model.HasImage = product.ImagePath != null;
            model.Status = product.Status;
            model.BlockedReason = product.BlockedReason;
        }

        public async Task<(FileKind? File, FileKind? Image)> ValidateAsync(ListingFormViewModel model, ModelStateDictionary modelState, bool isNew)
        {
            if (!Catalog.CategoriesFor(model.Type).Contains(model.Category))
            {
                modelState.AddModelError(nameof(model.Category), "Please choose a category.");
            }
            if (model.ListPrice.HasValue && model.ListPrice.Value > 0 && model.ListPrice.Value <= model.Price)
            {
                modelState.AddModelError(nameof(model.ListPrice), "The list price must be higher than the price to show a discount. Leave it empty otherwise.");
            }

            switch (model.Type)
            {
                case ProductType.Physical:
                case ProductType.Service:
                    if (model.Price < 0.01m)
                    {
                        modelState.AddModelError(nameof(model.Price), "Price must be at least $0.01.");
                    }
                    break;
            }

            if (model.Type == ProductType.Ebook)
            {
                if (string.IsNullOrWhiteSpace(model.AuthorName))
                {
                    modelState.AddModelError(nameof(model.AuthorName), "The Author name field is required.");
                }
                if (!Catalog.Languages.Contains(model.Language))
                {
                    modelState.AddModelError(nameof(model.Language), "Please choose a language.");
                }
            }

            if (model.Type == ProductType.Service)
            {
                if (model.DurationMinutes == null)
                {
                    modelState.AddModelError(nameof(model.DurationMinutes), "Please enter how long the service takes.");
                }
                if (model.ServiceLocation == null || !Enum.IsDefined(model.ServiceLocation.Value))
                {
                    modelState.AddModelError(nameof(model.ServiceLocation), "Please choose where the service is provided.");
                }
            }

            FileKind? fileKind = null;
            var isDownload = model.Type is ProductType.Ebook or ProductType.Digital;
            if (isDownload)
            {
                if (isNew && (model.File == null || model.File.Length == 0))
                {
                    modelState.AddModelError(nameof(model.File), "Please upload the file customers will download.");
                }
                fileKind = model.Type == ProductType.Ebook
                    ? await Uploads.CheckManuscriptAsync(model.File, nameof(model.File), modelState)
                    : await Uploads.CheckDigitalAsync(model.File, nameof(model.File), modelState);
            }

            var imageKind = await Uploads.CheckImageAsync(model.Image, nameof(model.Image), modelState);
            return (fileKind, imageKind);
        }

        // Copies form values onto the listing and stores any new files. Returns replaced
        // storage keys, to delete once the database change has been saved.
        public async Task<List<string?>> ApplyAsync(ListingFormViewModel model, Product product, FileKind? fileKind, FileKind? imageKind)
        {
            var replaced = new List<string?>();

            product.Type = model.Type;
            product.Name = model.Name.Trim();
            product.Subtitle = string.IsNullOrWhiteSpace(model.Subtitle) ? null : model.Subtitle.Trim();
            product.Description = model.Description.Trim();
            product.Category = model.Category;
            product.Price = Math.Round(model.Price, 2);
            product.ListPrice = model.ListPrice is > 0 ? Math.Round(model.ListPrice.Value, 2) : null;
            product.Stock = model.Type == ProductType.Physical ? model.Stock : 0;
            product.AuthorName = model.Type == ProductType.Ebook ? model.AuthorName?.Trim() : null;
            product.Language = model.Type == ProductType.Ebook ? model.Language : null;
            product.PageCount = model.Type == ProductType.Ebook ? model.PageCount : null;
            product.DurationMinutes = model.Type == ProductType.Service ? model.DurationMinutes : null;
            product.ServiceLocation = model.Type == ProductType.Service ? model.ServiceLocation : null;
            product.ServiceArea = model.Type == ProductType.Service && !string.IsNullOrWhiteSpace(model.ServiceArea) ? model.ServiceArea.Trim() : null;
            product.UpdatedAt = DateTime.UtcNow;

            if (fileKind != null)
            {
                var stored = await _storage.SaveAsync(model.File!, fileKind, "downloads");
                replaced.Add(product.FilePath);
                product.FilePath = stored.Key;
                product.FileName = Path.GetFileName(model.File!.FileName);
                product.FileContentType = stored.Kind.ContentType;
                product.FileSize = stored.Size;
            }

            if (imageKind != null)
            {
                var stored = await _storage.SaveAsync(model.Image!, imageKind, "images");
                replaced.Add(product.ImagePath);
                product.ImagePath = stored.Key;
                product.ImageContentType = stored.Kind.ContentType;
            }
            else if (model.RemoveImage && product.ImagePath != null)
            {
                replaced.Add(product.ImagePath);
                product.ImagePath = null;
                product.ImageContentType = null;
            }

            return replaced;
        }

        public void DeleteFiles(IEnumerable<string?> keys)
        {
            foreach (var key in keys)
            {
                _storage.Delete(key);
            }
        }
    }
}
