using Microsoft.AspNetCore.Hosting;

namespace SendSmsCallAlerts.Services
{
    public sealed record StoredMedia(string FileName, Uri Location);

    public interface IUploadedMediaStore
    {
        Task<List<StoredMedia>> SaveAsync(IEnumerable<IFormFile>? files, CancellationToken cancellationToken = default);
    }

    public sealed class UploadedMediaStore : IUploadedMediaStore
    {
        private const int MaxFileCount = 10;
        private const long MaxFileSize = 5 * 1024 * 1024;
        private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".gif"] = "image/gif"
            };

        private readonly string uploadDirectory;
        private readonly IMediaUrlFactory mediaUrlFactory;

        public UploadedMediaStore(IWebHostEnvironment environment, IMediaUrlFactory mediaUrlFactory)
        {
            uploadDirectory = Path.Combine(environment.WebRootPath, "uploads");
            this.mediaUrlFactory = mediaUrlFactory;
        }

        public async Task<List<StoredMedia>> SaveAsync(IEnumerable<IFormFile>? files, CancellationToken cancellationToken = default)
        {
            var selectedFiles = files?.Where(file => file != null && file.Length > 0).ToList() ?? new List<IFormFile>();
            if (selectedFiles.Count > MaxFileCount)
                throw new InvalidDataException($"A maximum of {MaxFileCount} images can be attached.");

            foreach (var file in selectedFiles)
            {
                var extension = Path.GetExtension(file.FileName);
                if (!AllowedTypes.TryGetValue(extension, out var allowedContentType)
                    || !string.Equals(file.ContentType, allowedContentType, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Only JPG, PNG, and GIF image files are supported.");
                if (file.Length > MaxFileSize)
                    throw new InvalidDataException("Each image must be 5 MB or smaller.");
            }

            Directory.CreateDirectory(uploadDirectory);
            var saved = new List<StoredMedia>();
            try
            {
                foreach (var file in selectedFiles)
                {
                    var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
                    var filePath = Path.Combine(uploadDirectory, fileName);
                    await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await file.CopyToAsync(stream, cancellationToken);
                    }

                    saved.Add(new StoredMedia(fileName, mediaUrlFactory.CreateUploadUrl(fileName)));
                }
            }
            catch
            {
                foreach (var media in saved)
                {
                    var filePath = Path.Combine(uploadDirectory, media.FileName);
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                throw;
            }

            return saved;
        }
    }
}
