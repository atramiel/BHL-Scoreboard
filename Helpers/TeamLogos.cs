using Scoreboard.Services;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Scoreboard.Helpers;

/// <summary>Loads team logos from the league bundle's disk cache as frozen ImageSources.</summary>
public static class TeamLogos
{
    public static async Task<ImageSource?> LoadAsync(string teamName)
    {
        var path = await LeagueSiteService.GetLogoPathAsync(teamName);
        return await FromCachedPathAsync(path);
    }

    /// <summary>Loads an arbitrary cached image (e.g. a bot photo) by its source URL.</summary>
    public static async Task<ImageSource?> LoadFromUrlAsync(string? url)
    {
        var path = await LeagueSiteService.GetImagePathAsync(url);
        return await FromCachedPathAsync(path);
    }

    /// <summary>Loads an image already sitting on local disk (e.g. an uploaded sponsor logo) — no download involved.</summary>
    public static Task<ImageSource?> LoadFromLocalPathAsync(string? path) => FromCachedPathAsync(path);

    private static Task<ImageSource?> FromCachedPathAsync(string? path)
    {
        try
        {
            if (path == null) return Task.FromResult<ImageSource?>(null);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelHeight = 320; // crisp at large sizes on a 1080p display
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return Task.FromResult<ImageSource?>(bitmap);
        }
        catch { return Task.FromResult<ImageSource?>(null); }
    }
}
