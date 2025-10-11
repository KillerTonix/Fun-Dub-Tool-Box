using Fun_Dub_Tool_Box.Utilities;
using Fun_Dub_Tool_Box.Utilities.Collections;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for ProcessingQueueWindow.xaml
    /// </summary>
    public partial class ProcessingQueueWindow : Window
    {
        private readonly Stopwatch _stopwatch = new();
        private CancellationTokenSource? _processingCts;
        private bool _shutdownRequested;
        private bool _synchronizingSelection;

        public ObservableCollection<ProcessingQueueItem> SequenceData { get; } = new();
        public ObservableCollection<string> AvailablePresets { get; } = new();

        public ProcessingQueueWindow()
        {
            InitializeComponent();
            DataContext = this;

            SequenceData.CollectionChanged += SequenceData_CollectionChanged;

            _shutdownRequested = AutomaticShutdownPC.IsChecked == true;

            ReloadPresets();
            LoadStoredQueue();

            UpdateButtonsState();
            UpdateUiForIdle();
        }

        private void LoadStoredQueue()
        {
            foreach (var job in QueueRepository.Load())
            {
                SequenceData.Add(new ProcessingQueueItem(job));
            }

            if (SequenceData.Any(i => i.Job.ShutdownWhenCompleted))
            {
                AutomaticShutdownPC.IsChecked = true;
            }
        }

        private void ReloadPresets()
        {
            AvailablePresets.Clear();
            foreach (var preset in PresetRepository.GetPresetNames())
            {
                AvailablePresets.Add(preset);
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ReloadPresets();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _processingCts?.Cancel();
        }

        private void SequenceData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
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

        private void ApplyPresetToItem(ProcessingQueueItem item)
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
                    var suggestedName = BuildSuggestedOutputName(item.Job, preset);
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

        private static string BuildSuggestedOutputName(RenderJob job, Preset preset)
        {
            var pattern = preset.General.FileNamePattern;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return job.Title + job.ContainerExt;
            }

            var title = string.IsNullOrWhiteSpace(job.Title) ? "Project" : job.Title;
            var result = pattern.Replace("{title}", title);

            // handle simple {date:format}
            result = Regex.Replace(
                result,
                "\\{date:(.+?)\\}",
                m => DateTime.Now.ToString(m.Groups[1].Value, CultureInfo.InvariantCulture));

            if (!result.EndsWith(job.ContainerExt, StringComparison.OrdinalIgnoreCase))
            {
                result += job.ContainerExt;
            }

            return result;
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
                item.Status = ProcessingStatus.Processing;
                CurrentRenderingFileLabel.Content = string.IsNullOrWhiteSpace(item.OutputPath)
                    ? item.SequenceFileName
                    : Path.GetFileName(item.OutputPath);

                const int steps = 20;
                for (int step = 1; step <= steps; step++)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(150, token);

                    double progressIndex = completed + step / (double)steps;
                    double percent = progressIndex / total * 100d;
                    OverallProgressBar.Value = percent;
                    UpdateTiming(progressIndex, total);
                }

                item.Status = ProcessingStatus.Completed;
                completed += 1;
            }

            _stopwatch.Stop();
            UpdateTiming(total, total);
            CurrentRenderingFileLabel.Content = "Queue complete";

            if (_shutdownRequested)
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
                FileName = BuildSuggestedOutputName(job, preset),
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

            var item = new ProcessingQueueItem(job)
            {
                PresetName = job.PresetName,
                Status = ProcessingStatus.Pending
            };

            SequenceData.Add(item);
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
          
            return selected.ToList();
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
            // SynchronizeSelectionFromGrid(); //need to fix this
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
    }

    public enum ProcessingStatus
    {
        Pending,
        Processing,
        Completed,
        Cancelled
    }

    public sealed class ProcessingQueueItem : INotifyPropertyChanged
    {
        private int _sequenceId;
        private string _sequenceFileName = string.Empty;
        private string _inputPath = string.Empty;
        private string _outputPath = string.Empty;
        private string _presetName = string.Empty;
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
