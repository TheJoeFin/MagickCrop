using MagickCrop.Models;
using MagickCrop.Services;
using MagickCrop.Windows;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MagickCrop.Controls;

public partial class WelcomeMessage : UserControl
{
    private readonly RecentProjectsManager _recentProjectsManager;

    public RoutedEventHandler PrimaryButtonEvent
    {
        get { return (RoutedEventHandler)GetValue(PrimaryButtonEventProperty); }
        set { SetValue(PrimaryButtonEventProperty, value); }
    }

    public static readonly DependencyProperty PrimaryButtonEventProperty =
        DependencyProperty.Register("PrimaryButtonEvent", typeof(RoutedEventHandler), typeof(WelcomeMessage), new PropertyMetadata(null));

    public WelcomeMessage()
    {
        InitializeComponent();
        _recentProjectsManager = new RecentProjectsManager();

        // Create command for project click
        RelayCommand<RecentProjectInfo> projectClickCommand = new(OpenProject);
        RelayCommand<RecentProjectInfo> projectDeleteCommand = new(RemoveProject);

        // Populate recent projects list
        UpdateRecentProjectsList(projectClickCommand, projectDeleteCommand);

        if (_recentProjectsManager.RecentProjects.Count > 0)
            RecentTab.IsSelected = true;
    }

    private void UpdateRecentProjectsList(ICommand projectClickCommand, ICommand projectDeleteCommand)
    {
        RecentProjectsList.Items.Clear();

        foreach (RecentProjectInfo project in _recentProjectsManager.RecentProjects)
        {
            RecentProjectItem item = new()
            {
                Project = project,
                ProjectClickedCommand = projectClickCommand,
                ProjectDeletedCommand = projectDeleteCommand,
                Margin = new Thickness(5)
            };

            RecentProjectsList.Items.Add(item);
        }

        // Show/hide the "no projects" message
        NoRecentProjectsMessage.Visibility =
            _recentProjectsManager.RecentProjects.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenProject(RecentProjectInfo? project)
    {
        if (project == null) return;

        // Close the welcome screen
        WelcomeBorder.Visibility = Visibility.Collapsed;

        // Get the main window and open the project
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.LoadMeasurementsPackageFromFile(project.PackagePath);
        }
    }

    private async void RemoveProject(RecentProjectInfo? project)
    {
        if (project is null)
            return;

        Wpf.Ui.Controls.MessageBox uiMessageBox = new()
        {
            Title = "Confirm delete",
            Content = $"Remove '{project.Name}' from recent projects?",
            PrimaryButtonText = "Remove",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
        };
        Wpf.Ui.Controls.MessageBoxResult result = await uiMessageBox.ShowDialogAsync();

        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            _recentProjectsManager.RemoveProject(project.Id, true);

            // Update the UI
            RelayCommand<RecentProjectInfo> projectClickCommand = new(OpenProject);
            RelayCommand<RecentProjectInfo> projectDeleteCommand = new(RemoveProject);
            UpdateRecentProjectsList(projectClickCommand, projectDeleteCommand);
        }
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.JoeFinApps.com",
            UseShellExecute = true
        });
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeBorder.Visibility = Visibility.Collapsed;
        PrimaryButtonEvent?.Invoke(sender, e);
    }

    private void SourceLink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/TheJoeFin/MagickCrop",
            UseShellExecute = true
        });
    }

    private void AboutHyperbuttons_Click(object sender, RoutedEventArgs e)
    {
        AboutWindow aboutWindow = new();
        aboutWindow.ShowDialog();
    }

    private async void OpenPackageButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeBorder.Visibility = Visibility.Collapsed;

        bool opened = false;
        if (Window.GetWindow(this) is MainWindow mainWindow)
            opened = await mainWindow.LoadMeasurementsPackageFromFile();

        if (!opened)
            WelcomeBorder.Visibility = Visibility.Visible;
    }
}
