using FFMpegCore;
using Fun_Dub_Tool_Box.Utilities;
using Fun_Dub_Tool_Box.Utilities.Collections;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for ProcessingQueueWindow.xaml
    /// </summary>
    public partial class ProcessingQueueWindow : Window
    {
        private readonly FfmpegRenderService _renderService = new();
        private readonly Stopwatch _stopwatch = new();
        private CancellationTokenSource? _processingCts;
        private bool _shutdownRequested;
        private bool _isLoadingQueue;

        public ObservableCollection<ProcessingQueueItem> SequenceData { get; } = [];
        public ObservableCollection<string> AvailablePresets { get; } = [];

        public ProcessingQueueWindow()
        {
            InitializeComponent();
            DataContext = this;
            QueueGrid.ItemsSource = SequenceData;
            SequenceData.CollectionChanged += SequenceData_CollectionChanged;

            _shutdownRequested = AutomaticShutdownPC.IsChecked == true;

            ReloadPresets();
            LoadStoredQueue();

            UpdateButtonsState();
            UpdateUiForIdle();
        }

        private void LoadStoredQueue()
        {
            _isLoadingQueue = true;

            try
            {
                SequenceData.Clear();

                foreach (var job in QueueRepository.Load())
                {
                    if (job.Status == ProcessingStatus.Processing)
                    {
                        job.Status = ProcessingStatus.Cancelled;
                    }

                    SequenceData.Add(new ProcessingQueueItem(job));
                }
            }
            finally
            {
                _isLoadingQueue = false;
            }

            RefreshSequenceIds();
            UpdateButtonsState();
            UpdateQueueCountLabel();
            PersistQueue();
        }

        private void ReloadPresets()
        {
            AvailablePresets.Clear();
            foreach (var preset in PresetRepository.GetPresetNames())
            {
                AvailablePresets.Add(preset);
            }
        }


        private void SequenceData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isLoadingQueue)
            {
                if (e.NewItems != null)
                {
                    foreach (ProcessingQueueItem item in e.NewItems)
                    {
                        item.PropertyChanged -= QueueItem_PropertyChanged;
                        item.PropertyChanged += QueueItem_PropertyChanged;
                    }
                }

                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in SequenceData)
                {
                    item.PropertyChanged -= QueueItem_PropertyChanged;
                    item.PropertyChanged += QueueItem_PropertyChanged;
                }
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (ProcessingQueueItem item in e.OldItems)
                    {
                        item.PropertyChanged -= QueueItem_PropertyChanged;
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (ProcessingQueueItem item in e.NewItems)
                    {
                        item.PropertyChanged += QueueItem_PropertyChanged;
                        ApplyPresetToItem(item);
                    }
                }
            }

            RefreshSequenceIds();
            UpdateButtonsState();
            PersistQueue();
            UpdateQueueCountLabel();
        }

        private void QueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ProcessingQueueItem item)
            {
                if (e.PropertyName == nameof(ProcessingQueueItem.PresetName))
                {
                    ApplyPresetToItem(item);
                }

                PersistQueue();
            }
        }

        private static void ApplyPresetToItem(ProcessingQueueItem item)
        {
            if (string.IsNullOrWhiteSpace(item.PresetName))
            {
                return;
            }

            if (PresetRepository.TryLoadPreset(item.PresetName, out var preset))
            {
                var extension = "." + preset.General.Container.ToString();
                item.Job.ContainerExt = extension;

                if (string.IsNullOrWhiteSpace(item.OutputPath))
                {
                    var suggestedName = RenderJobHelper.BuildSuggestedOutputName(item.Job, preset);
                    item.OutputPath = Path.Combine(item.Job.OutputFolder, suggestedName);
                }
                else
                {
                    var currentExt = Path.GetExtension(item.OutputPath);
                    if (!string.Equals(currentExt, extension, StringComparison.OrdinalIgnoreCase))
                    {
                        item.OutputPath = Path.ChangeExtension(item.OutputPath, extension);
                    }
                }
            }
        }

        private void RefreshSequenceIds()
        {
            for (int i = 0; i < SequenceData.Count; i++)
            {
                SequenceData[i].SequenceID = i + 1;
            }
        }

        private void UpdateButtonsState()
        {
            bool hasItems = SequenceData.Count > 0;
            bool processing = _processingCts != null;
            bool hasSelection = GetSelectedQueueItems().Count > 0;

            RemoveFromQueueButton.IsEnabled = hasItems && hasSelection && !processing;
            EditeSelectedQueueButton.IsEnabled = hasItems && hasSelection && !processing;
            ClearQueueButton.IsEnabled = hasItems && !processing;
            StartProcessingButton.IsEnabled = hasItems && !processing;
        }

        private void UpdateQueueCountLabel()
        {
            var mainWindow = GetHostMainWindow();
            mainWindow?.UpdateQueueCount(SequenceData.Count);
        }

        private void UpdateUiForIdle()
        {
            OverallProgressBar.Value = 0;
            ElapsedTimeLabel.Content = "00:00:00";
            RemainingTimeLabel.Content = "00:00:00";
            CurrentRenderingFileLabel.Content = "Idle";
        }

        private async void StartProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SequenceData.Any())
            {
                MessageBox.Show("Add items to the queue before starting processing.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _processingCts = new CancellationTokenSource();
            UpdateButtonsState();

            try
            {
                await ProcessQueueAsync(_processingCts.Token);
            }
            catch (OperationCanceledException)
            {
                foreach (var item in SequenceData.Where(i => i.Status == ProcessingStatus.Processing))
                {
                    item.Status = ProcessingStatus.Cancelled;
                }
            }
            finally
            {
                _processingCts = null;
                UpdateButtonsState();
                if (!SequenceData.Any() || SequenceData.All(i => i.Status != ProcessingStatus.Processing))
                {
                    UpdateUiForIdle();
                }
            }
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            _stopwatch.Restart();
            double completed = 0;
            int total = SequenceData.Count;

            foreach (var item in SequenceData)
            {
                token.ThrowIfCancellationRequested();

                if (!EnsurePresetAvailable(item, out var preset))
                {
                    item.Status = ProcessingStatus.Failed;
                    completed += 1;
                    UpdateProgress(completed, total);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.OutputPath))
                {
                    item.Status = ProcessingStatus.Failed;
                    MessageBox.Show($"The queue item '{item.SequenceFileName}' does not have a valid output path.", "Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    completed += 1;
                    UpdateProgress(completed, total);
                    continue;
                }

                item.Status = ProcessingStatus.Processing;
                CurrentRenderingFileLabel.Content = string.IsNullOrWhiteSpace(item.OutputPath)
                    ? item.SequenceFileName
                    : Path.GetFileName(item.OutputPath);

                TimeSpan itemDuration = TimeSpan.Zero;
                var progress = new Progress<FFMpegProgress>(progressValue =>
                {
                    var fraction = CalculateProgressFraction(progressValue, itemDuration);
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatistics(progressValue);
                        UpdateProgress(completed + fraction, total);
                    });
                });

                try
                {
                    await _renderService.RenderAsync(item.Job, preset, progress, duration => itemDuration = duration, token);
                    item.Status = ProcessingStatus.Completed;
                    completed += 1;
                    UpdateProgress(completed, total);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    item.Status = ProcessingStatus.Failed;
                    completed += 1;
                    UpdateProgress(completed, total);
                    MessageBox.Show($"Failed to render '{item.SequenceFileName}': {ex.Message}", "Queue", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            _stopwatch.Stop();
            UpdateProgress(total, total);
            CurrentRenderingFileLabel.Content = total > 0 ? "Queue complete" : "Idle";

            if (_shutdownRequested && SequenceData.Any(i => i.Status == ProcessingStatus.Completed))
            {
                RequestShutdown();
            }
        }

        private void UpdateTiming(double processed, int total)
        {
            ElapsedTimeLabel.Content = FormatTime(_stopwatch.Elapsed);

            if (processed <= 0 || processed >= total)
            {
                RemainingTimeLabel.Content = "00:00:00";
                return;
            }

            double averageTicks = _stopwatch.Elapsed.Ticks / processed;
            if (averageTicks <= 0)
            {
                RemainingTimeLabel.Content = "00:00:00";
                return;
            }

            long remainingTicks = (long)Math.Round(averageTicks * (total - processed));
            RemainingTimeLabel.Content = FormatTime(TimeSpan.FromTicks(Math.Max(0, remainingTicks)));
        }

        private static string FormatTime(TimeSpan span) => span.ToString(span.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss");

        private void UpdateProgress(double progressIndex, int total)
        {
            if (total <= 0)
            {
                OverallProgressBar.Value = 0;
                UpdateTiming(0, 0);
                return;
            }

            double percent = Math.Clamp(progressIndex / total * 100d, 0d, 100d);
            OverallProgressBar.Value = percent;
            UpdateTiming(progressIndex, total);
        }

        private bool EnsurePresetAvailable(ProcessingQueueItem item, out Preset preset)
        {
            if (PresetRepository.TryLoadPreset(item.PresetName, out preset))
            {
                return true;
            }

            MessageBox.Show($"Preset '{item.PresetName}' could not be loaded. Remove the item or choose a different preset.", "Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static double CalculateProgressFraction(FFMpegProgress progress, TimeSpan totalDuration)
        {
            if (TryGetProgressValue(progress, "Percent", out double percent))
            {
                if (percent > 1)
                {
                    percent /= 100d;
                }

                return Math.Clamp(percent, 0d, 1d);
            }

            if (TryGetProgressValue(progress, "Progress", out double fractionalProgress))
            {
                if (fractionalProgress > 1)
                {
                    fractionalProgress /= 100d;
                }

                return Math.Clamp(fractionalProgress, 0d, 1d);
            }

            if (totalDuration > TimeSpan.Zero)
            {
                if (TryGetProgressValue(progress, "OutTime", out TimeSpan outTime))
                {
                    return Math.Clamp(outTime.TotalSeconds / totalDuration.TotalSeconds, 0d, 1d);
                }

                if (TryGetProgressValue(progress, "CurrentTime", out TimeSpan currentTime))
                {
                    return Math.Clamp(currentTime.TotalSeconds / totalDuration.TotalSeconds, 0d, 1d);
                }
            }

            return 0d;
        }

        private void UpdateStatistics(FFMpegProgress progress)
        {
            if (CurrentFrameLabel == null)
            {
                return;
            }

            if (TryGetProgressValue(progress, "Frame", out long frame))
            {
                CurrentFrameLabel.Content = $"Current Frame: {frame}";
            }

            if (TryGetProgressValue(progress, "Fps", out double fps))
            {
                FPSLabel.Content = "FPS: " + fps.ToString("0.##", CultureInfo.InvariantCulture);
            }
            else if (TryGetProgressValue(progress, "Speed", out double speed))
            {
                FPSLabel.Content = "Speed: " + speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
            }

            if (TryGetProgressValue(progress, "Bitrate", out double bitrateDouble))
            {
                BitrateLabel.Content = "Bitrate: " + Math.Max(0, bitrateDouble).ToString("0.##", CultureInfo.InvariantCulture) + " kbps";
            }
            else if (TryGetProgressValue(progress, "Bitrate", out long bitrateLong))
            {
                BitrateLabel.Content = "Bitrate: " + Math.Max(0, bitrateLong).ToString(CultureInfo.InvariantCulture) + " kbps";
            }
        }

        private static bool TryGetProgressValue<T>(FFMpegProgress progress, string propertyName, out T value)
        {
            var property = typeof(FFMpegProgress).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                var raw = property.GetValue(progress);
                if (raw is T typed)
                {
                    value = typed;
                    return true;
                }

                if (raw is null)
                {
                    value = default!;
                    return false;
                }

                if (raw is string stringValue)
                {
                    if (typeof(T) == typeof(double) && double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                    {
                        value = (T)(object)parsedDouble;
                        return true;
                    }

                    if (typeof(T) == typeof(long) && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
                    {
                        value = (T)(object)parsedLong;
                        return true;
                    }

                    if (typeof(T) == typeof(TimeSpan) && TimeSpan.TryParse(stringValue, CultureInfo.InvariantCulture, out var parsedTimeSpan))
                    {
                        value = (T)(object)parsedTimeSpan;
                        return true;
                    }
                }

                if (typeof(T) == typeof(TimeSpan) && raw is TimeSpan timeSpan)
                {
                    value = (T)(object)timeSpan;
                    return true;
                }

                if (typeof(T) == typeof(TimeSpan) && raw is TimeSpan? nullable && nullable.HasValue)
                {
                    value = (T)(object)nullable.Value;
                    return true;
                }

                if (typeof(T) == typeof(double) && raw is double doubleValue)
                {
                    value = (T)(object)doubleValue;
                    return true;
                }

                if (typeof(T) == typeof(double) && raw is double? nullableDouble && nullableDouble.HasValue)
                {
                    value = (T)(object)nullableDouble.Value;
                    return true;
                }

                try
                {
                    value = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    // Ignore conversion errors and fall through to the default return.
                }
            }

            value = default!;
            return false;
        }

        private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetHostMainWindow();
            if (mainWindow == null)
            {
                MessageBox.Show("Open the main window to configure a project before adding to the queue.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!mainWindow.TryBuildRenderJobTemplate(out var job, out var error))
            {
                MessageBox.Show(error, "Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!PresetRepository.TryLoadPreset(job.PresetName, out var preset))
            {
                MessageBox.Show($"Preset '{job.PresetName}' could not be loaded.", "Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            job.ContainerExt = "." + preset.General.Container.ToString();
            job.OutputFolder = string.IsNullOrEmpty(job.OutputFolder)
                ? (Path.GetDirectoryName(job.MainVideoPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                : job.OutputFolder;

            var dialog = new SaveFileDialog
            {
                Title = "Choose output file",
                FileName = RenderJobHelper.BuildSuggestedOutputName(job, preset),
                Filter = $"{preset.General.Container.ToString().ToUpperInvariant()}|*{job.ContainerExt}|All files|*.*",
                InitialDirectory = Directory.Exists(job.OutputFolder) ? job.OutputFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (SequenceData.Any(q => string.Equals(q.OutputPath, dialog.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("That output file is already queued.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            job.OutputPath = dialog.FileName;
            job.OutputFolder = Path.GetDirectoryName(dialog.FileName) ?? job.OutputFolder;
            job.Status = ProcessingStatus.Pending;
            job.ShutdownWhenCompleted = _shutdownRequested;

            if (!TryEnqueueJob(job, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        public bool TryEnqueueJob(RenderJob job, out string? errorMessage)
        {
            errorMessage = null;

            if (SequenceData.Any(q => string.Equals(q.OutputPath, job.OutputPath, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "That output file is already queued.";
                return false;
            }

            var item = new ProcessingQueueItem(job)
            {
                PresetName = job.PresetName,
                Status = ProcessingStatus.Pending
            };

            SequenceData.Add(item);
            return true;
        }

        private MainWindow? GetHostMainWindow()
        {
            return Owner as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        }

        private void PersistQueue()
        {
            QueueRepository.Save(SequenceData.Select(i => i.Job));
        }

        private List<ProcessingQueueItem> GetSelectedQueueItems()
        {
            var selected = new HashSet<ProcessingQueueItem>();

            if (QueueGrid?.SelectedItems != null)
            {
                foreach (ProcessingQueueItem item in QueueGrid.SelectedItems)
                {
                    selected.Add(item);
                }
            }

            return [.. selected];
        }

        private void RemoveFromQueueButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedQueueItems();
            if (selectedItems.Count == 0)
            {
                return;
            }

            foreach (var item in selectedItems)
            {
                SequenceData.Remove(item);
            }
        }

        private void QueueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonsState();
        }

        private void SequentialProcessingListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonsState();
        }





        private void EditSelectedQueueButton_Click(object sender, RoutedEventArgs e)
        {
            var item = GetSelectedQueueItems().FirstOrDefault();
            if (item == null)
            {
                MessageBox.Show("Select a queue item to edit.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var filter = GetFilterForItem(item);

            var dialog = new SaveFileDialog
            {
                Title = "Select output path",
                FileName = Path.GetFileName(item.OutputPath),
                Filter = filter,
                InitialDirectory = !string.IsNullOrEmpty(item.OutputPath)
                    ? Path.GetDirectoryName(item.OutputPath)
                    : (Directory.Exists(item.Job.OutputFolder) ? item.Job.OutputFolder : Path.GetDirectoryName(item.InputPath))
            };

            if (dialog.ShowDialog() == true)
            {
                item.OutputPath = dialog.FileName;
            }
        }

        private static string GetFilterForItem(ProcessingQueueItem item)
        {
            var ext = item.Job.ContainerExt;
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = Path.GetExtension(item.OutputPath);
            }

            ext = string.IsNullOrWhiteSpace(ext) ? ".mp4" : ext;
            return $"{ext.Trim('.').ToUpperInvariant()}|*{ext}|All files|*.*";
        }

        private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SequenceData.Any())
            {
                return;
            }

            if (MessageBox.Show("Clear the entire processing queue?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SequenceData.Clear();
                UpdateUiForIdle();
            }
        }

        private void QueueGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            if (e.Column.Header?.ToString() == "Output File" && e.EditingElement is TextBox textBox && e.Row.Item is ProcessingQueueItem item)
            {
                item.OutputPath = textBox.Text;
            }
        }

        private void AutomaticShutdownPC_Checked(object sender, RoutedEventArgs e)
        {
            _shutdownRequested = true;
            foreach (var item in SequenceData)
            {
                item.Job.ShutdownWhenCompleted = true;
            }
            PersistQueue();
        }

        private void AutomaticShutdownPC_Unchecked(object sender, RoutedEventArgs e)
        {
            _shutdownRequested = false;
            foreach (var item in SequenceData)
            {
                item.Job.ShutdownWhenCompleted = false;
            }
            PersistQueue();
        }

        private static void RequestShutdown()
        {
            try
            {
                var psi = new ProcessStartInfo("shutdown", "/s /t 60")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to schedule shutdown: {ex.Message}", "Shutdown", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private ProcessingQueueItem? SelectedItem => QueueGrid.SelectedItem as ProcessingQueueItem;


        private void Ctx_Rename_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Ctx_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;
            // keep reference before removing from collection
            var item = SelectedItem;
            SequenceData.Remove(item);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            QueueGrid.Items.Refresh();
        }

        private void RenderingEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedItem == null) return;
            int selectedEngine = RenderingEngineComboBox.SelectedIndex;
            if (selectedEngine < 0 || selectedEngine > 2) selectedEngine = 0;
            if (selectedEngine == 0)
                SelectedItem.Job.GpuAcceleration = true;
            else if (selectedEngine == 1)
                SelectedItem.Job.GpuAcceleration = false;
            PersistQueue();
        }
    }

    public enum ProcessingStatus
    {
        Pending,
        Processing,
        Completed,
        Cancelled,
        Failed
    }

    public sealed class ProcessingQueueItem : INotifyPropertyChanged
    {
        private int _sequenceId;
        private string _sequenceFileName = string.Empty;
        private string _inputPath = string.Empty;
        private string _outputPath = string.Empty;
        private string _presetName = string.Empty;
        private bool _GpuAccelerationEnabled = true;
        private ProcessingStatus _status = ProcessingStatus.Pending;

        public ProcessingQueueItem(RenderJob job)
        {
            Job = job;
            _sequenceId = job.SequenceId;
            _sequenceFileName = string.IsNullOrWhiteSpace(job.Title)
                ? Path.GetFileName(job.MainVideoPath)
                : job.Title;
            _inputPath = job.MainVideoPath;
            _outputPath = job.OutputPath;
            _presetName = job.PresetName;
            _GpuAccelerationEnabled = job.GpuAcceleration;
            _status = job.Status;
        }

        public RenderJob Job { get; }

        public int SequenceID
        {
            get => _sequenceId;
            set
            {
                if (SetProperty(ref _sequenceId, value))
                {
                    Job.SequenceId = value;
                }
            }
        }

        public string SequenceFileName
        {
            get => _sequenceFileName;
            set
            {
                if (SetProperty(ref _sequenceFileName, value ?? string.Empty))
                {
                    Job.Title = value ?? string.Empty;
                }
            }
        }

        public string InputPath
        {
            get => _inputPath;
            set
            {
                if (SetProperty(ref _inputPath, value ?? string.Empty))
                {
                    Job.MainVideoPath = value ?? string.Empty;
                }
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (SetProperty(ref _outputPath, value ?? string.Empty))
                {
                    Job.OutputPath = value ?? string.Empty;
                    Job.OutputFolder = string.IsNullOrEmpty(value)
                        ? string.Empty
                        : Path.GetDirectoryName(value) ?? string.Empty;
                }
            }
        }

        public string PresetName
        {
            get => _presetName;
            set
            {
                if (SetProperty(ref _presetName, value ?? string.Empty))
                {
                    Job.PresetName = value ?? string.Empty;
                }
            }
        }

        public bool GpuAccelerationEnabled
        {
            get => _GpuAccelerationEnabled;
            set
            {
                if (SetProperty(ref _GpuAccelerationEnabled, value))
                {
                    Job.GpuAcceleration = value;
                }
            }
        }

        public ProcessingStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    Job.Status = value;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
