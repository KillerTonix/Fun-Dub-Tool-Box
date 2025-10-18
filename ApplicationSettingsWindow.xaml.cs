using System.Windows;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for ApplicationSettingsWindow.xaml
    /// </summary>
    public partial class ApplicationSettingsWindow : Window
    {
        public ApplicationSettingsWindow()
        {
            InitializeComponent();
            UpdateIdleDelayState();
        }

        private void StartQueueAutomaticallyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateIdleDelayState();
        }

        private void StartQueueAutomaticallyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateIdleDelayState();
        }

        private void UpdateIdleDelayState()
        {
            if (IdleDelayMinutesComboBox != null)
            {
                IdleDelayMinutesComboBox.IsEnabled = StartQueueAutomaticallyCheckBox?.IsChecked == true;
            }
        }
    }
}
