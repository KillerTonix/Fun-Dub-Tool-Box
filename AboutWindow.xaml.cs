using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using ModernWpf;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public ObservableCollection<ChangeLogEntry> ChangeLogEntries { get; } = new()
        {
            new ChangeLogEntry(
                "Version 0.4.0",
                "May 2024",
                "Refined preset handling, introduced GPU-aware optimisations, and improved application stability while processing batches."),
            new ChangeLogEntry(
                "Version 0.3.0",
                "April 2024",
                "Enhanced the rendering pipeline with clearer status reporting, smoothing queue progress and introducing resilient cancellation."),
            new ChangeLogEntry(
                "Version 0.2.0",
                "March 2024",
                "Expanded preset and queue management capabilities, unlocking quicker configuration swaps for common project types."),
            new ChangeLogEntry(
                "Version 0.1.0",
                "February 2024",
                "Initial public release featuring material toggles, subtitle management, and a streamlined dubbing workspace."),
        };

        public AboutWindow()
        {
            InitializeComponent();
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            DataContext = this;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionTextBlock.Text = version is null ? "Version 1.0.0" : $"Version {version}";

            if (ChangeLogEntries.Count > 0)
            {
                LastUpdatedTextBlock.Text = $"Last updated {ChangeLogEntries[0].Date}";
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Uri?.AbsoluteUri))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                };

                Process.Start(psi);
                e.Handled = true;
            }
        }
    }

    public record ChangeLogEntry(string Version, string Date, string Summary);
}
