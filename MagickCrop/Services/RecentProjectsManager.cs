using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagickCrop.Services;

/// <summary>
/// Manages saving, loading and tracking of recent projects
/// </summary>
public class RecentProjectsManager
{
    private const string ProjectIndexFileName = "project_index.json";
    private const string ThumbnailsFolder = "Thumbnails";
    private readonly string _appDataFolder;
    private readonly string _projectsFolder;
    private readonly string _thumbnailsFolder;
    private readonly int _maxRecentProjects = 10;

    public ObservableCollection<RecentProjectInfo> RecentProjects { get; private set; } = [];

    public RecentProjectsManager()
    {
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MagickCrop");

        _projectsFolder = Path.Combine(_appDataFolder, "Projects");
        _thumbnailsFolder = Path.Combine(_appDataFolder, ThumbnailsFolder);

        Directory.CreateDirectory(_appDataFolder);
        Directory.CreateDirectory(_projectsFolder);
        Directory.CreateDirectory(_thumbnailsFolder);

        if (RecentProjects.Count == 0)
            LoadRecentProjects();
    }

    /// <summary>
    /// Loads the list of recent projects from the index file
    /// </summary>
    private void LoadRecentProjects()
    {
        string indexPath = Path.Combine(_appDataFolder, ProjectIndexFileName);
        if (!File.Exists(indexPath))
            return;

        RecentProjects.Clear();

        try
        {
            string json = File.ReadAllText(indexPath);
            List<RecentProjectInfo>? projects = JsonSerializer.Deserialize<List<RecentProjectInfo>>(json);

            if (projects is null)
                return;

            // Remove projects with missing package files
            projects = [.. projects
                .Where(p => !string.IsNullOrEmpty(p.PackagePath) && File.Exists(p.PackagePath))
                .DistinctBy(p => p.Name)];

            // Load thumbnails for each project
            foreach (RecentProjectInfo project in projects)
                project.LoadThumbnail();

            RecentProjects = [.. projects];
        }
        catch (Exception)
        {
            // If loading fails, start with an empty list
            RecentProjects = [];
        }
    }

    /// <summary>
    /// Saves the current list of recent projects to the index file
    /// </summary>
    private void SaveRecentProjectsList()
    {
        string indexPath = Path.Combine(_appDataFolder, ProjectIndexFileName);

        try
        {
            string json = JsonSerializer.Serialize(RecentProjects.ToList());
            File.WriteAllText(indexPath, json);
        }
        catch (Exception)
        {
            // Just continue if saving fails
        }
    }

    /// <summary>
    /// Creates a thumbnail from the current image
    /// </summary>
    /// <param name="imageSource">Source image to create thumbnail from</param>
    /// <param name="projectId">Project ID for naming the thumbnail</param>
    /// <returns>Path to the created thumbnail</returns>
    public string CreateThumbnail(BitmapSource imageSource, string projectId)
    {
        if (imageSource == null || string.IsNullOrEmpty(projectId))
            return string.Empty;

        string thumbnailPath = Path.Combine(_thumbnailsFolder, $"{projectId}_thumb.jpg");

        try
        {
            // Create a smaller version for the thumbnail
            int thumbnailWidth = 200;
            double scale = thumbnailWidth / imageSource.Width;
            int thumbnailHeight = (int)(imageSource.Height * scale);

            TransformedBitmap resizedImage = new(
                imageSource,
                new ScaleTransform(scale, scale)
            );

            JpegBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(resizedImage));

            using (FileStream fileStream = new(thumbnailPath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }

            return thumbnailPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Autosaves the current project
    /// </summary>
    /// <param name="package">The measurement package to save</param>
    /// <param name="imageSource">The current image source for thumbnail generation</param>
    /// <returns>The project info</returns>
    public async Task<RecentProjectInfo?> AutosaveProject(MagickCropMeasurementPackage package, BitmapSource? imageSource)
    {
        if (package is null || string.IsNullOrEmpty(package.ImagePath))
            return null;

        package.Metadata ??= new();

        string projectId = package.Metadata?.ProjectId ?? Guid.NewGuid().ToString();
        package.Metadata.ProjectId = projectId;
        package.Metadata.LastModified = DateTime.Now;

        // Set project name
        string projectName = string.IsNullOrEmpty(package.Metadata.OriginalFilename)
            ? "Untitled Project"
            : package.Metadata.OriginalFilename;

        // Create project path
        string packagePath = Path.Combine(_projectsFolder, $"{projectId}.mcm");

        // Create thumbnail
        string thumbnailPath = string.Empty;
        if (imageSource is not null)
            thumbnailPath = CreateThumbnail(imageSource, projectId);

        // Save the package
        bool saveSuccess = await package.SaveToFileAsync(packagePath);
        if (!saveSuccess)
            return null;

        // Create and add to recent projects
        RecentProjectInfo projectInfo = new()
        {
            Id = projectId,
            Name = projectName,
            PackagePath = packagePath,
            ThumbnailPath = thumbnailPath,
            LastModified = package.Metadata.LastModified
        };

        projectInfo.LoadThumbnail();
        UpdateRecentProjectsList(projectInfo);

        return projectInfo;
    }

    /// <summary>
    /// Updates the recent projects list with the given project
    /// </summary>
    /// <param name="projectInfo">The project to add or update</param>
    private void UpdateRecentProjectsList(RecentProjectInfo projectInfo)
    {
        // Remove the project if it already exists in the list
        RecentProjectInfo? existingProject = RecentProjects.FirstOrDefault(p => p.Id == projectInfo.Id);
        if (existingProject is not null)
        {
            RecentProjects.Remove(existingProject);
        }

        // Add the project at the beginning of the list
        RecentProjects.Insert(0, projectInfo);

        // Trim the list if it exceeds the maximum number of projects
        while (RecentProjects.Count > _maxRecentProjects)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }

        // Save the updated list
        SaveRecentProjectsList();
    }

    /// <summary>
    /// Removes a project from the recent projects list and optionally deletes its files
    /// </summary>
    /// <param name="projectId">The ID of the project to remove</param>
    /// <param name="deleteFiles">Whether to delete the project's files</param>
    public void RemoveProject(string projectId, bool deleteFiles = true)
    {
        // Find and remove from the list
        RecentProjectInfo? project = RecentProjects.FirstOrDefault(p => p.Id == projectId);
        if (project is null)
            return;

        RecentProjects.Remove(project);
        SaveRecentProjectsList();

        if (!deleteFiles)
            return;

        try
        {
            if (File.Exists(project.PackagePath))
                File.Delete(project.PackagePath);

            if (File.Exists(project.ThumbnailPath))
                File.Delete(project.ThumbnailPath);
        }
        catch (Exception)
        {
            // Continue even if deletion fails
        }
    }
}
