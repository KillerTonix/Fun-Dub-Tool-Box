using FD_Tool_Box.Utilities.Collections;
using ModernWpf;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            MaterialsListView.ItemsSource = MaterialsData;
            this.DataContext = this;

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;

        }

        private void AddIntroToggleButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteMaterialButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}