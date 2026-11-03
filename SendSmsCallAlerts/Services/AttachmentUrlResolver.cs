namespace SendSmsCallAlerts.Services
{
    public enum AttachmentUrlSource
    {
        None,
        Remote,
        Local
    }

    public static class AttachmentUrlResolver
    {
        public static AttachmentUrlSource ResolveSource(string? location, string? fileName)
        {
            if (IsRemoteLocation(location))
            {
                return AttachmentUrlSource.Remote;
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return AttachmentUrlSource.Local;
            }

            return AttachmentUrlSource.None;
        }

        public static bool IsRemoteLocation(string? location)
        {
            return !string.IsNullOrWhiteSpace(location)
                && Uri.TryCreate(location, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
