namespace SendSmsCallAlerts.Services
{
    public interface IMediaUrlFactory
    {
        Uri CreateUploadUrl(string fileName);
        Uri CreateUrl(string relativePath);
    }

    public sealed class MediaUrlFactory : IMediaUrlFactory
    {
        private readonly Uri baseUri;

        public MediaUrlFactory(IConfiguration configuration)
        {
            var configuredAddress = configuration["AppSettings:WebAddress"]?.Trim();
            if (string.IsNullOrWhiteSpace(configuredAddress))
                throw new InvalidOperationException("AppSettings:WebAddress must be configured to create media URLs.");

            if (!configuredAddress.Contains("://", StringComparison.Ordinal))
                configuredAddress = "https://" + configuredAddress;

            if (!Uri.TryCreate(configuredAddress.TrimEnd('/') + "/", UriKind.Absolute, out baseUri!))
                throw new InvalidOperationException("AppSettings:WebAddress must be a valid absolute base URL.");
        }

        public Uri CreateUploadUrl(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("A safe uploaded file name is required.", nameof(fileName));

            return CreateUrl("uploads/" + Uri.EscapeDataString(fileName));
        }

        public Uri CreateUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Uri.TryCreate(relativePath, UriKind.Absolute, out _)
                || relativePath.StartsWith("/", StringComparison.Ordinal)
                || relativePath.Contains('\\'))
                throw new ArgumentException("A relative media path is required.", nameof(relativePath));

            return new Uri(baseUri, relativePath);
        }
    }
}
