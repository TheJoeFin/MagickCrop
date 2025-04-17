using System.IO;
using System.Windows.Media.Imaging;

namespace MagickCrop.Models;

public class RecentProjectInfo
{
    /// <summary>
    /// Unique identifier for the project
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the project (usually derived from the filename)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to the package file for this project
    /// </summary>
    public string PackagePath { get; set; } = string.Empty;

    /// <summary>
    /// When the project was last modified/saved
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// Path to the thumbnail file
    /// </summary>
    public string ThumbnailPath { get; set; } = string.Empty;

    /// <summary>
    /// Thumbnail image for display in the recent projects gallery
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public BitmapImage? Thumbnail { get; set; }

    /// <summary>
    /// Loads the thumbnail image
    /// </summary>
    public void LoadThumbnail()
    {
        if (!string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath))
        {
            try
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(ThumbnailPath);
                bitmap.EndInit();
                Thumbnail = bitmap;
            }
            catch
            {
                // If loading fails, we'll just have no thumbnail
                Thumbnail = null;
            }
        }
    }
}
