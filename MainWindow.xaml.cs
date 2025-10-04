using FD_Tool_Box.Utilities.Collections;
using ModernWpf;
using System.Collections.ObjectModel;
using System.Windows;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<MaterialsListData> MaterialsData { get; set; } = [];

        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            MaterialsListView.ItemsSource = MaterialsData;
            this.DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void AddIntroToggleButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteMaterialButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var аpplicationSettingsWindow = new ApplicationSettingsWindow { Owner = this }; // Create a new instance and set the owner to the current window
            аpplicationSettingsWindow.ShowDialog(); // Show window as a dialog
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {           
            var аboutWindow = new AboutWindow { Owner = this }; // Create a new instance and set the owner to the current window
            аboutWindow.ShowDialog(); // Show window as a dialog
        }

        private void PresetConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            var presetConfigurationWindow = new PresetConfigurationWindow { Owner = this }; // Create a new instance and set the owner to the current window
            presetConfigurationWindow.ShowDialog(); // Show window as a dialog
        }

        private void LogoSetManualToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            var positioningLogoWindow = new PositioningLogoWindow { Owner = this }; // Create a new instance and set the owner to the current window
            positioningLogoWindow.ShowDialog(); // Show window as a dialog
        }

        private void MoreSettingsToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            var additionalProjectSettingsWindow = new AdditionalProjectSettingsWindow { Owner = this }; // Create a new instance and set the owner to the current window
            additionalProjectSettingsWindow.ShowDialog(); // Show window as a dialog
        }

        private void SeeQueueListButton_Click(object sender, RoutedEventArgs e)
        {
            var processingQueueWindow = new ProcessingQueueWindow { Owner = this }; // Create a new instance and set the owner to the current window
            processingQueueWindow.ShowDialog(); // Show window as a dialog
        }

        private void MaterialsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}