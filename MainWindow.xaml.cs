using FFMpegCore;
using Fun_Dub_Tool_Box.Utilities.Collections;
using ModernWpf;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<MaterialItem> _materials = [];


        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            this.DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MaterialsGrid.ItemsSource = _materials;
            RefreshIndices();
        }
        private void RefreshIndices()
        {
            for (int i = 0; i < _materials.Count; i++) _materials[i].Index = i + 1;
            MaterialsGrid.Items.Refresh();
        }

        private async void AddMaterial(MaterialType type)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Select {type}",
                Filter = type switch
                {
                    MaterialType.Logo => "Images|*.png;*.jpg;*.jpeg",
                    MaterialType.Subtitles => "Subtitles|*.srt;*.ass;*.vtt",
                    MaterialType.Audio => "Audio|*.wav;*.mp3;*.aac;*.m4a;*.flac",
                    _ => "Video|*.mp4;*.mkv;*.mov;*.avi;*.m4v"
                },
                Multiselect = (type == MaterialType.Audio) // multiple bgm if you want
            };

            if (dlg.ShowDialog() != true) return;

            // Validate one-per-type for singleton assets
            if (type is MaterialType.Intro or MaterialType.Video or MaterialType.Outro or MaterialType.Logo or MaterialType.Subtitles)
            {
                if (_materials.Any(m => m.Type == type))
                {
                    MessageBox.Show($"{type} already added. Replace it from context menu.", "Limit", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            var files = dlg.FileNames;
            foreach (var f in files)
                _materials.Add(await CreateItemAsync(type, f));

            RefreshIndices();
        }

        private static string FormatDuration(TimeSpan ts) => ts == TimeSpan.Zero ? "" : ts.ToString(ts.TotalHours >= 1 ? @"hh\:mm\:ss\.fff" : @"mm\:ss\.fff");

        private static async Task<MaterialItem> CreateItemAsync(MaterialType type, string path)
        {
            var item = new MaterialItem { Type = type, Path = path };

            try
            {
                // Probe only when media (skip for images/subs)
                if (type is MaterialType.Logo)
                {
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load the image immediately
                    bitmap.EndInit();

                    item.Resolution = $"{bitmap.PixelWidth}x{bitmap.PixelHeight}";
                    item.Duration = "";
                    item.Extra = "Image";
                    return item;
                }

                if (type is MaterialType.Subtitles)
                {
                    item.Extra = Path.GetExtension(path).ToLowerInvariant().Trim('.');
                    return item;
                }

                var info = await FFProbe.AnalyseAsync(path);

                // video stream info
                var v = info.PrimaryVideoStream;
                if (v != null)
                {
                    var fps = v.FrameRate == 0 ? "" : $"@{Math.Round(v.FrameRate, 2)}";
                    item.Resolution = $"{v.Width}x{v.Height}{fps}";
                }
                // duration
                item.Duration = FormatDuration(info.Duration);

                // audio stream info
                var a = info.PrimaryAudioStream;
                if (a != null)
                    item.Extra = $"{a.CodecName} • {a.SampleRateHz / 1000.0:0.#}kHz • {a.Channels}ch";
            }
            catch (Exception ex)
            {
                item.Extra = "Probe failed";
                Debug.WriteLine(ex);
            }
            return item;
        }

        private void AddIntroToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddMaterial(MaterialType.Intro);
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
            var editPresetWindow = new EditPresetWindow { Owner = this }; // Create a new instance and set the owner to the current window
            editPresetWindow.ShowDialog(); // Show window as a dialog
        }

        private void LogoSetManualToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            var positioningLogoWindow = new PositioningLogoWindow { Owner = this }; // Create a new instance and set the owner to the current window
            positioningLogoWindow.ShowDialog(); // Show window as a dialog
        }

        private void SeeQueueListButton_Click(object sender, RoutedEventArgs e)
        {
            var processingQueueWindow = new ProcessingQueueWindow { Owner = this }; // Create a new instance and set the owner to the current window
            processingQueueWindow.ShowDialog(); // Show window as a dialog
        }


        private void MaterialsGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void MaterialsGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

            foreach (var f in files)
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                MaterialType type = ext switch
                {
                    ".png" or ".jpg" or ".jpeg" => MaterialType.Logo,
                    ".srt" or ".ass" or ".vtt" => MaterialType.Subtitles,
                    ".wav" or ".mp3" or ".aac" or ".m4a" or ".flac" => MaterialType.Audio,
                    _ => MaterialType.Video
                };
                // Respect singletons
                if (type != MaterialType.Audio && _materials.Any(m => m.Type == type))
                    continue;

                _materials.Add(await CreateItemAsync(type, f));
            }
            RefreshIndices();
        }
        private MaterialItem? SelectedItem => MaterialsGrid.SelectedItem as MaterialItem;

        private void MaterialsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Ctx_Open_Click(sender, e);


        private void Ctx_Open_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;
            try { Process.Start(new ProcessStartInfo(SelectedItem.Path) { UseShellExecute = true }); } catch { }

        }

        private void Ctx_Reveal_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;
            var arg = "/select,\"" + SelectedItem.Path + "\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arg));
        }

        private async void Ctx_Replace_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;
            var type = SelectedItem.Type;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Replace {type}",
                Filter = type switch
                {
                    MaterialType.Logo => "Images|*.png;*.jpg;*.jpeg",
                    MaterialType.Subtitles => "Subtitles|*.srt;*.ass;*.vtt",
                    MaterialType.Audio => "Audio|*.wav;*.mp3;*.aac;*.m4a;*.flac",
                    _ => "Video|*.mp4;*.mkv;*.mov;*.avi;*.m4v"
                }
            };
            if (dlg.ShowDialog() != true) return;

            var idx = _materials.IndexOf(SelectedItem);
            _materials[idx] = await CreateItemAsync(type, dlg.FileName);
            RefreshIndices();
        }

        private void Ctx_Remove_Click(object sender, RoutedEventArgs e) => DeleteRow_Click(sender, e);


        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;
            // keep reference before removing from collection
            var item = SelectedItem;
            _materials.Remove(SelectedItem);

            //uncheck buttons
            switch (item.Type)
            {
                case MaterialType.Intro:
                    AddIntroToggleButton.IsChecked = false; break;
                case MaterialType.Video:
                    AddVideoToggleButton.IsChecked = false; break;
                case MaterialType.Logo:
                    AddLogoToggleButton.IsChecked = false; break;
                case MaterialType.Subtitles:
                    AddSubtitlesToggleButton.IsChecked = false; break;
                case MaterialType.Audio:
                    AddAuidioToggleButton.IsChecked = false; break;
                case MaterialType.Outro:
                    AddOutroToggleButton.IsChecked = false; break;
            }
            RefreshIndices();
        }

        private void AddVideoToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddMaterial(MaterialType.Video);
        }

        private bool Has(MaterialType t) => _materials.Any(m => m.Type == t);

        private void AddLogoToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddMaterial(MaterialType.Logo);
        }

        private void AddSubtitlesToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddMaterial(MaterialType.Subtitles);
        }

        private void AddAuidioToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddMaterial(MaterialType.Audio);
        }

        private void AddOutroToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddMaterial(MaterialType.Outro);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var intro = _materials.FirstOrDefault(m => m.Type == MaterialType.Intro)?.Path;
            var main = _materials.FirstOrDefault(m => m.Type == MaterialType.Video)?.Path;
            var outro = _materials.FirstOrDefault(m => m.Type == MaterialType.Outro)?.Path;
            var logo = _materials.FirstOrDefault(m => m.Type == MaterialType.Logo)?.Path;
            var subs = _materials.FirstOrDefault(m => m.Type == MaterialType.Subtitles)?.Path;
            var audios = _materials.Where(m => m.Type == MaterialType.Audio).Select(m => m.Path).ToList();

            // validate
            if (string.IsNullOrEmpty(main))
            {
                MessageBox.Show("Please add a main VIDEO.", "Missing media");
                return;
            }
            // enqueue your render job with these paths…

        }

        private void AddIntroToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var intro = _materials.FirstOrDefault(m => m.Type == MaterialType.Intro);
            if (intro != null) _materials.Remove(intro);
        }

        private void AddVideoToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var video = _materials.FirstOrDefault(m => m.Type == MaterialType.Video);
            if (video != null) _materials.Remove(video);
        }

        private void AddLogoToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var logo = _materials.FirstOrDefault(m => m.Type == MaterialType.Logo);
            if (logo != null) _materials.Remove(logo);
        }

        private void AddSubtitlesToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var subs = _materials.FirstOrDefault(m => m.Type == MaterialType.Subtitles);
            if (subs != null) _materials.Remove(subs);
        }

        private void AddAuidioToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var auidio = _materials.FirstOrDefault(m => m.Type == MaterialType.Audio);
            if (auidio != null) _materials.Remove(auidio);
        }

        private void AddOutroToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var outro = _materials.FirstOrDefault(m => m.Type == MaterialType.Outro);
            if (outro != null) _materials.Remove(outro);
        }
    }
}