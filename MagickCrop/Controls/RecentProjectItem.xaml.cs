using MagickCrop.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MagickCrop.Controls;

public partial class RecentProjectItem : UserControl
{
    public static readonly DependencyProperty ProjectProperty =
        DependencyProperty.Register(
            "Project", 
            typeof(RecentProjectInfo), 
            typeof(RecentProjectItem), 
            new PropertyMetadata(null, OnProjectChanged));

    public static readonly DependencyProperty ProjectClickedCommandProperty =
        DependencyProperty.Register(
            "ProjectClickedCommand",
            typeof(ICommand),
            typeof(RecentProjectItem));

    public static readonly DependencyProperty ProjectDeletedCommandProperty =
        DependencyProperty.Register(
            "ProjectDeletedCommand",
            typeof(ICommand),
            typeof(RecentProjectItem));

    public RecentProjectInfo? Project
    {
        get { return (RecentProjectInfo)GetValue(ProjectProperty); }
        set { SetValue(ProjectProperty, value); }
    }

    public ICommand ProjectClickedCommand
    {
        get { return (ICommand)GetValue(ProjectClickedCommandProperty); }
        set { SetValue(ProjectClickedCommandProperty, value); }
    }

    public ICommand ProjectDeletedCommand
    {
        get { return (ICommand)GetValue(ProjectDeletedCommandProperty); }
        set { SetValue(ProjectDeletedCommandProperty, value); }
    }

    public RecentProjectItem()
    {
        InitializeComponent();
        this.MouseLeftButtonUp += RecentProjectItem_MouseLeftButtonUp;
    }

    private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RecentProjectItem control && e.NewValue is RecentProjectInfo project)
        {
            control.UpdateUI(project);
        }
    }

    private void UpdateUI(RecentProjectInfo project)
    {
        ProjectNameTextBlock.Text = project.Name;
        
        string timeAgo = GetTimeAgo(project.LastModified);
        LastModifiedTextBlock.Text = timeAgo;
        
        if (project.Thumbnail != null)
        {
            ThumbnailImage.Source = project.Thumbnail;
        }
    }

    private void RecentProjectItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Project != null && ProjectClickedCommand != null && ProjectClickedCommand.CanExecute(Project))
        {
            ProjectClickedCommand.Execute(Project);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent the click from being handled by the item click handler
        
        if (Project != null && ProjectDeletedCommand != null && ProjectDeletedCommand.CanExecute(Project))
        {
            ProjectDeletedCommand.Execute(Project);
        }
    }

    private string GetTimeAgo(DateTime dateTime)
    {
        TimeSpan span = DateTime.Now - dateTime;

        if (span.TotalDays > 30)
        {
            return $"Edited {dateTime:MMM d, yyyy}";
        }
        if (span.TotalDays > 1)
        {
            return $"Edited {(int)span.TotalDays} days ago";
        }
        if (span.TotalHours > 1)
        {
            return $"Edited {(int)span.TotalHours} hours ago";
        }
        if (span.TotalMinutes > 1)
        {
            return $"Edited {(int)span.TotalMinutes} minutes ago";
        }
        
        return "Edited just now";
    }
}
