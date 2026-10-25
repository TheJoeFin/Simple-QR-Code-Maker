namespace Simple_QR_Code_Maker.Models;

public static class CommonBrandCatalog
{
    public static IReadOnlyList<CommonBrandPreset> All { get; } = Array.AsReadOnly<CommonBrandPreset>(
    [
        new("YouTube", "youtube", 0xED1D24, "youtube.com", "youtu.be"),
        new("Bluesky", "bluesky", 0x006AFF, "bsky.app"),
        new("LinkedIn", "linkedin", 0x0A66C2, "linkedin.com", "lnkd.in"),
        new("Facebook", "facebook", 0x1877F2, "facebook.com", "fb.com", "fb.watch"),
        new("Twitter", "twitter", 0x0C7ABF, "twitter.com"),
        new("X", "x", 0x000000, "x.com"),
        new("Google Maps", "google_maps", 0x1967D2, "google.com/maps", "maps.google.com", "maps.app.goo.gl", "goo.gl/maps"),
        new("Instagram", "instagram", 0xC13584, "instagram.com"),
        new("TikTok", "tiktok", 0x000000, "tiktok.com"),
        new("WhatsApp", "whatsapp", 0x075E54, "whatsapp.com", "wa.me"),
        new("Spotify", "spotify", 0x191414, "spotify.com", "spotify.link"),
        new("GitHub", "github", 0x181717, "github.com"),
        new("Reddit", "reddit", 0xD93900, "reddit.com", "redd.it"),
        new("Twitch", "twitch", 0x6441A5, "twitch.tv"),
    ]);

    public static CommonBrandPreset? FindMatching(string content)
    {
        string text = content.Trim();
        if (text.Length == 0 || text.Any(char.IsWhiteSpace))
            return null;

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        string host = uri.IdnHost.TrimEnd('.');
        foreach (CommonBrandPreset brand in All)
        {
            foreach (string pattern in brand.UrlPatterns)
            {
                int pathStart = pattern.IndexOf('/');
                string brandHost = pathStart < 0 ? pattern : pattern[..pathStart];
                if (!host.Equals(brandHost, StringComparison.OrdinalIgnoreCase)
                    && !host.EndsWith("." + brandHost, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (pathStart < 0)
                    return brand;

                string brandPath = pattern[pathStart..];
                string path = uri.AbsolutePath;
                if (path.Equals(brandPath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(brandPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return brand;
                }
            }
        }

        return null;
    }
}