using FFMpegCore;
using Fun_Dub_Tool_Box.Utilities;
using Fun_Dub_Tool_Box.Utilities.Collections;
using ModernWpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ObservableCollection<MaterialItem> _materials = [];
        public ObservableCollection<string> PresetNames { get; } = [];
        private string? _selectedPresetName;
        private bool _isReloadingPresets;
        private readonly LogoSettings _logoSettings = new();
        private bool _updatingLogoControls;


        public string? SelectedPresetName
        {
            get => _selectedPresetName;
            set
            {
                if (string.Equals(_selectedPresetName, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedPresetName = value;

                if (_isReloadingPresets)
                {
                    return;
                }

                OnPropertyChanged();
            }
        }

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
            LoadPresetNames();
            UpdateQueueCount(QueueRepository.Count());
            ApplyLogoSettingsToUi();
        }
        private void RefreshIndices()
        {
            for (int i = 0; i < _materials.Count; i++) _materials[i].Index = i + 1;
            MaterialsGrid.Items.Refresh();
            UpdateLogoControlsAvailability();
        }

        private void LoadPresetNames(string? desiredSelection = null)
        {
            var previousSelection = _selectedPresetName;
            _isReloadingPresets = true;

            try
            {
                var names = PresetRepository.GetPresetNames();
                PresetNames.Clear();
                foreach (var name in names)
                {
                    PresetNames.Add(name);
                }

                string? selection = desiredSelection ?? previousSelection;
                if (!string.IsNullOrWhiteSpace(selection) && PresetNames.Contains(selection))
                {
                    _selectedPresetName = selection;
                }
                else
                {
                    _selectedPresetName = PresetNames.FirstOrDefault();
                }
            }
            finally
            {
                _isReloadingPresets = false;
            }

            OnPropertyChanged(nameof(SelectedPresetName));
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
        public void UpdateQueueCount(int count)
        {
            ItemsInQueueLabel.Content = $"Items in queue: {count}";
        }

        private void ApplyLogoSettingsToUi()
        {
            if (LeftTopBtn == null)
            {
                return;
            }

            _updatingLogoControls = true;
            try
            {
                bool manual = _logoSettings.UseManualPlacement;
                LeftTopBtn.IsChecked = !manual && _logoSettings.Anchor == LogoAnchor.TopLeft;
                RightTopBtn.IsChecked = !manual && _logoSettings.Anchor == LogoAnchor.TopRight;
                LeftBottomBtn.IsChecked = !manual && _logoSettings.Anchor == LogoAnchor.BottomLeft;
                RightBottomBtn.IsChecked = !manual && _logoSettings.Anchor == LogoAnchor.BottomRight;
            }
            finally
            {
                _updatingLogoControls = false;
            }

            UpdateLogoTransparencyText();
        }

        private void UpdateLogoTransparencyText()
        {
            if (LogoTransparencyTextBox == null)
            {
                return;
            }

            var percent = Math.Clamp(Math.Round(_logoSettings.Opacity * 100), 0, 100);
            LogoTransparencyTextBox.Text = percent.ToString("0", CultureInfo.InvariantCulture) + "%";
        }


        private void UpdateLogoControlsAvailability()
        {
            bool hasLogo = _materials.Any(m => m.Type == MaterialType.Logo);

            foreach (var button in GetLogoAnchorButtons())
            {
                button.IsEnabled = hasLogo;
            }

            if (LogoTransparencyTextBox != null)
            {
                LogoTransparencyTextBox.IsEnabled = hasLogo;
            }

            if (LogoSetManualToggleButton != null)
            {
                if (!hasLogo)
                {
                    LogoSetManualToggleButton.IsChecked = false;
                }
                LogoSetManualToggleButton.IsEnabled = hasLogo;
            }
        }

        private IEnumerable<ToggleButton> GetLogoAnchorButtons()
        {
            yield return LeftTopBtn;
            yield return RightTopBtn;
            yield return LeftBottomBtn;
            yield return RightBottomBtn;
        }

        private void LogoAnchorButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_updatingLogoControls)
            {
                return;
            }

            if (sender is ToggleButton button && button.Tag is string tag && Enum.TryParse(tag, out LogoAnchor anchor))
            {
                _logoSettings.ApplyAnchor(anchor);
                ApplyLogoSettingsToUi();
            }
        }

        private void LogoAnchorButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_updatingLogoControls || _logoSettings.UseManualPlacement)
            {
                return;
            }

            if (sender is ToggleButton button)
            {
                button.IsChecked = true;
            }
        }

        private void LogoTransparencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyTransparencyFromText();
        }

        private void LogoTransparencyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyTransparencyFromText();
                e.Handled = true;
            }
        }

        private void ApplyTransparencyFromText()
        {
            if (LogoTransparencyTextBox == null)
            {
                return;
            }

            var text = (LogoTransparencyTextBox.Text ?? string.Empty).Trim();
            if (text.EndsWith('%'))
            {
                text = text[..^1];
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                value = Math.Clamp(value, 0, 100);
                _logoSettings.Opacity = value / 100.0;
            }

            UpdateLogoTransparencyText();
        }

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
            var previousSelection = SelectedPresetName;
            var editPresetWindow = new EditPresetWindow { Owner = this }; // Create a new instance and set the owner to the current window
            editPresetWindow.ShowDialog(); // Show window as a dialog
            LoadPresetNames(previousSelection);
        }


        private void LogoSetManualToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            var logo = _materials.FirstOrDefault(m => m.Type == MaterialType.Logo);
            if (logo == null)
            {
                MessageBox.Show("Add a logo before configuring manual placement.", "Logo", MessageBoxButton.OK, MessageBoxImage.Information);
                LogoSetManualToggleButton.IsChecked = false;
                return;
            }

            var positioningLogoWindow = new PositioningLogoWindow(_logoSettings, logo.Path) { Owner = this };
            positioningLogoWindow.ShowDialog();
            LogoSetManualToggleButton.IsChecked = true;
            ApplyLogoSettingsToUi();
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

        public bool TryBuildRenderJobTemplate(out RenderJob job, out string error)
        {
            job = new RenderJob();
            error = string.Empty;

            var mainVideo = _materials.FirstOrDefault(m => m.Type == MaterialType.Video);
            if (mainVideo == null)
            {
                error = "Please add a main VIDEO before adding to the queue.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedPresetName))
            {
                error = "Select a render preset before adding to the queue.";
                return false;
            }

            if (!PresetRepository.TryLoadPreset(SelectedPresetName, out var preset))
            {
                error = $"Preset '{SelectedPresetName}' could not be loaded.";
                return false;
            }

            job.PresetName = SelectedPresetName;
            job.MainVideoPath = mainVideo.Path;
            job.Title = Path.GetFileNameWithoutExtension(mainVideo.Path);
            job.ContainerExt = "." + preset.General.Container.ToString();
            job.OutputFolder = Path.GetDirectoryName(mainVideo.Path) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            job.Materials = [.. _materials.Select(CloneMaterial)];
            job.Status = ProcessingStatus.Pending;
            job.Logo = _logoSettings.Clone();

            return true;
        }

        private static MaterialItem CloneMaterial(MaterialItem item) => new()
        {
            Index = item.Index,
            Type = item.Type,
            Path = item.Path,
            Duration = item.Duration,
            Resolution = item.Resolution,
            Extra = item.Extra
        };

        private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildRenderJobTemplate(out var job, out var error))
            {
                MessageBox.Show(error, "Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryPromptForOutputFile(job, out bool cancelled, out var promptError))
            {
                if (!cancelled && !string.IsNullOrWhiteSpace(promptError))
                {
                    MessageBox.Show(promptError, "Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return;
            }

            var queueWindow = GetProcessingQueueWindow();
            if (queueWindow != null)
            {
                if (!queueWindow.TryEnqueueJob(job, out var enqueueError))
                {
                    MessageBox.Show(enqueueError ?? "That output file is already queued.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else if (!TryPersistJobToRepository(job, out var persistError))
            {
                MessageBox.Show(persistError ?? "That output file is already queued.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateQueueCount(QueueRepository.Count());
            MessageBox.Show("Render job added to the queue.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static bool TryPromptForOutputFile(RenderJob job, out bool cancelled, out string? errorMessage)
        {
            cancelled = false;
            errorMessage = null;

            if (!PresetRepository.TryLoadPreset(job.PresetName, out var preset))
            {
                errorMessage = $"Preset '{job.PresetName}' could not be loaded.";
                return false;
            }

            job.ContainerExt = "." + preset.General.Container.ToString();
            job.OutputFolder = string.IsNullOrEmpty(job.OutputFolder)
                ? (Path.GetDirectoryName(job.MainVideoPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                : job.OutputFolder;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Choose output file",
                FileName = RenderJobHelper.BuildSuggestedOutputName(job, preset),
                Filter = $"{preset.General.Container.ToString().ToUpperInvariant()}|*{job.ContainerExt}|All files|*.*",
                InitialDirectory = Directory.Exists(job.OutputFolder)
                    ? job.OutputFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() != true)
            {
                cancelled = true;
                return false;
            }

            if (string.IsNullOrWhiteSpace(dialog.FileName))
            {
                errorMessage = "Choose a valid output file.";
                return false;
            }

            job.OutputPath = dialog.FileName;
            job.OutputFolder = Path.GetDirectoryName(dialog.FileName) ?? job.OutputFolder;
            job.Status = ProcessingStatus.Pending;

            return true;
        }

        private static bool TryPersistJobToRepository(RenderJob job, out string? errorMessage)
        {
            errorMessage = null;

            var existingJobs = QueueRepository.Load().ToList();
            if (existingJobs.Any(j => string.Equals(j.OutputPath, job.OutputPath, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "That output file is already queued.";
                return false;
            }

            var nextSequence = existingJobs.Count == 0 ? 1 : existingJobs.Max(j => j.SequenceId) + 1;
            job.SequenceId = nextSequence;
            existingJobs.Add(job);
            QueueRepository.Save(existingJobs);
            return true;
        }

        private static ProcessingQueueWindow? GetProcessingQueueWindow()
        {
            return Application.Current.Windows.OfType<ProcessingQueueWindow>().FirstOrDefault();
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
            var processingQueueWindow = new ProcessingQueueWindow { Owner = this }; // Create a new instance and set the owner to the current window
            processingQueueWindow.ShowDialog(); // Show window as a dialog

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


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            string QueueDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FunDubToolBox", "Queue");
            string QueueFilePath = Path.Combine(QueueDirectory, "queue.json");
            if (File.Exists(QueueFilePath))
            {
                try
                {
                    File.Delete(QueueFilePath);
                }
                catch
                {
                    // Ignore any errors during deletion
                }
            }
        }
    }
}