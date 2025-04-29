using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Represents a package containing both image and measurement data for MagickCrop
/// </summary>
public class MagickCropMeasurementPackage
{
    private const string MetadataFileName = "metadata.json";
    private const string MeasurementsFileName = "measurements.json";
    private const string ImageFileName = "image.jpg";

    /// <summary>
    /// Metadata for the package
    /// </summary>
    public PackageMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Collection of measurement data
    /// </summary>
    public MeasurementCollection Measurements { get; set; } = new();

    /// <summary>
    /// Path to the image file
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Save the package to a .mcm file (zip archive with measurements and image)
    /// </summary>
    /// <param name="packagePath">Path to save the package to</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool SaveToFileAsync(string packagePath)
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
            return false;

        try
        {
            // Create a temporary directory for package contents
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Save metadata
                string metadataPath = Path.Combine(tempDir, MetadataFileName);
                File.WriteAllText(metadataPath, JsonSerializer.Serialize(Metadata));

                // Save measurements
                string measurementsPath = Path.Combine(tempDir, MeasurementsFileName);
                Measurements.SaveToFile(measurementsPath);

                // Copy image 
                string imagePath = Path.Combine(tempDir, ImageFileName);
                File.Copy(ImagePath, imagePath);

                // Create zip archive
                if (File.Exists(packagePath))
                    File.Delete(packagePath);

                ZipFile.CreateFromDirectory(tempDir, packagePath);
                return true;
            }
            finally
            {
                // Clean up temporary directory
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Silently ignore cleanup failures
                    }
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Load a package from a .mcm file
    /// </summary>
    /// <param name="packagePath">Path to the .mcm package file</param>
    /// <returns>A MagickCropMeasurementPackage object or null if loading fails</returns>
    public static MagickCropMeasurementPackage? LoadFromFileAsync(string packagePath)
    {
        if (!File.Exists(packagePath))
            return null;

        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string tempImagePath = string.Empty;

        try
        {
            // Extract the archive
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(packagePath, tempDir, true);

            // Create the package
            MagickCropMeasurementPackage package = new();

            // Load metadata if available
            string metadataPath = Path.Combine(tempDir, MetadataFileName);
            if (File.Exists(metadataPath))
            {
                string metadataJson = File.ReadAllText(metadataPath);
                package.Metadata = JsonSerializer.Deserialize<PackageMetadata>(metadataJson) ?? new PackageMetadata();
            }

            // Load measurements
            string measurementsPath = Path.Combine(tempDir, MeasurementsFileName);
            if (File.Exists(measurementsPath))
            {
                package.Measurements = MeasurementCollection.LoadFromFile(measurementsPath) ?? new MeasurementCollection();
            }

            // Extract image to a separate temp file that will persist
            string packageImagePath = Path.Combine(tempDir, ImageFileName);
            if (File.Exists(packageImagePath))
            {
                tempImagePath = Path.GetTempFileName();
                    tempImagePath = Path.ChangeExtension(tempImagePath, ".jpg");
                File.Copy(packageImagePath, tempImagePath, true);
                package.ImagePath = tempImagePath;
            }

            return package;
        }
        catch (Exception)
        {
            // On failure, clean up any extracted image file
            if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
            {
                try { File.Delete(tempImagePath); } catch { /* ignore */ }
            }
            return null;
        }
        finally
        {
            // Clean up temporary directory
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }
    }
}
