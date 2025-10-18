using Fun_Dub_Tool_Box.Utilities.Collections;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for PresetConfigurationWindow.xaml
    /// </summary>
    public partial class EditPresetWindow : Window
    {
        private Preset _preset = new();
        private readonly string PresetsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "FunDubToolBox", "Presets"); //AppData\Roaming\FunDubToolBox\Presets
        public EditPresetWindow(Preset? presetFromCaller = null)
        {
            InitializeComponent();
            if (presetFromCaller != null) _preset = presetFromCaller;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // populate simple enum combos
            V_Codec.ItemsSource = Enum.GetValues<VideoCodec>();
            V_PixFmt.ItemsSource = Enum.GetValues<PixelFormat>();
            V_Hw.ItemsSource = Enum.GetValues<HardwareEncoder>();
            V_Rate.ItemsSource = Enum.GetValues<RateControl>();
            A_Codec.ItemsSource = Enum.GetValues<AudioCodec>();
            A_Sample.ItemsSource = new[] { 44100, 48000 };
            A_Channels.ItemsSource = new[] { 1, 2 };
            G_Container.ItemsSource = Enum.GetValues<Container>();

            // preset name dropdown (existing files)
            Directory.CreateDirectory(PresetsDir);
            PresetNameCmb.ItemsSource = null;
            PresetNameCmb.Items.Clear();

            var presetFiles = Directory.GetFiles(PresetsDir, "*.json")
                                       .Select(Path.GetFileNameWithoutExtension)
                                       .ToList();

            PresetNameCmb.ItemsSource = presetFiles;
            PresetNameCmb.IsEditable = true;          // if you want free text
            PresetNameCmb.Text = _preset.Name;        // set current name

            // slider display
            V_CRF.ValueChanged += (_, __) => V_CRFVal.Text = ((int)V_CRF.Value).ToString();

            ApplyPresetToUI(_preset);
            UpdateSummary();
        }

        private void V_Rate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var rc = (RateControl)V_Rate.SelectedItem!;
            CRFPanel.Visibility = rc == RateControl.CRF ? Visibility.Visible : Visibility.Collapsed;
            BitratePanel.Visibility = rc == RateControl.CRF ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ApplyPresetToUI(Preset p)
        {
            // video
            V_Width.Text = p.Video.Width.ToString();
            V_Height.Text = p.Video.Height.ToString();
            V_Fps.Text = p.Video.Fps.ToString("0.###");
            V_Codec.SelectedItem = p.Video.Codec;
            V_PixFmt.SelectedItem = p.Video.PixelFormat;
            V_Hw.SelectedItem = p.Video.Hardware;
            V_Rate.SelectedItem = p.Video.RateControl;
            V_CRF.Value = p.Video.CRF;
            V_Bitrate.Text = p.Video.BitrateKbps.ToString();
            V_TwoPass.IsChecked = p.Video.TwoPass;
            V_Profile.Text = p.Video.Profile;
            V_Level.Text = p.Video.Level;
            V_FullRange.IsChecked = p.General.UseColorRangeFull;

            // audio
            A_Codec.SelectedItem = p.Audio.Codec;
            A_Bitrate.Text = p.Audio.BitrateKbps.ToString();
            A_Sample.SelectedItem = p.Audio.SampleRate;
            A_Channels.SelectedItem = p.Audio.Channels;
            A_Normalize.IsChecked = p.Audio.Normalize;
            A_Lufs.Text = p.Audio.TargetLufs.ToString();
            A_TP.Text = p.Audio.TruePeakDb.ToString();
            A_Lra.Text = p.Audio.Lra.ToString();

            // general
            G_Container.SelectedItem = p.General.Container;
            G_FastStart.IsChecked = p.General.FastStart;
            G_FilePattern.Text = string.IsNullOrWhiteSpace(p.General.FileNamePattern)
                                       ? "{title}_{date:yyyyMMdd_HHmm}.mp4"
                                       : p.General.FileNamePattern;

            V_Rate_SelectionChanged(null!, null!);
        }

        private Preset ReadPresetFromUI()
        {
            var p = new Preset
            {
                Name = string.IsNullOrWhiteSpace(PresetNameCmb.Text) ? _preset.Name : PresetNameCmb.Text
            };

            // video
            p.Video.Codec = (VideoCodec)V_Codec.SelectedItem!;
            p.Video.Width = int.Parse(V_Width.Text);
            p.Video.Height = int.Parse(V_Height.Text);
            p.Video.Fps = double.Parse(V_Fps.Text.Replace(',', '.'));
            p.Video.PixelFormat = (PixelFormat)V_PixFmt.SelectedItem!;
            p.Video.Hardware = (HardwareEncoder)V_Hw.SelectedItem!;
            p.Video.RateControl = (RateControl)V_Rate.SelectedItem!;
            p.Video.CRF = (int)V_CRF.Value;
            p.Video.BitrateKbps = int.Parse(V_Bitrate.Text);
            p.Video.TwoPass = V_TwoPass.IsChecked == true;
            p.Video.Profile = V_Profile.Text;
            p.Video.Level = V_Level.Text;

            // audio
            p.Audio.Codec = (AudioCodec)A_Codec.SelectedItem!;
            p.Audio.BitrateKbps = int.Parse(A_Bitrate.Text);
            p.Audio.SampleRate = (int)A_Sample.SelectedItem!;
            p.Audio.Channels = (int)A_Channels.SelectedItem!;
            p.Audio.Normalize = A_Normalize.IsChecked == true;
            p.Audio.TargetLufs = double.Parse(A_Lufs.Text.Replace(',', '.'));
            p.Audio.TruePeakDb = double.Parse(A_TP.Text.Replace(',', '.'));
            p.Audio.Lra = double.Parse(A_Lra.Text.Replace(',', '.'));

            // general
            p.General.Container = (Container)G_Container.SelectedItem!;
            p.General.FastStart = G_FastStart.IsChecked == true;
            p.General.FileNamePattern = G_FilePattern.Text;
            p.General.UseColorRangeFull = V_FullRange.IsChecked == true;

            return p;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _preset = ReadPresetFromUI();
            Directory.CreateDirectory(PresetsDir);
            var path = Path.Combine(PresetsDir, $"{Sanitize(_preset.Name)}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(_preset, new JsonSerializerOptions { WriteIndented = true }));
            UpdateSummary();
            // refresh name dropdown if new
            var list = (PresetNameCmb.ItemsSource as List<string>) ?? [];
            if (!list.Contains(_preset.Name))
            {
                list.Add(_preset.Name);
                PresetNameCmb.ItemsSource = null;   // clear binding first
                PresetNameCmb.ItemsSource = list;   // re-bind updated list
            }

        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var name = string.IsNullOrWhiteSpace(PresetNameCmb.Text) ? _preset.Name : PresetNameCmb.Text;
            var path = Path.Combine(PresetsDir, $"{Sanitize(name)}.json");
            if (File.Exists(path)) File.Delete(path);

            // refresh name dropdown
            var list = (PresetNameCmb.ItemsSource as List<string>) ?? [];
            if (!list.Contains(name))
            {
                return;
            }
            list.Remove(name);
            PresetNameCmb.ItemsSource = null;   // clear binding first
            PresetNameCmb.ItemsSource = list;   // re-bind updated list
            PresetNameCmb.SelectedIndex = 0;    // select first if any
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control &&
                e.Key == System.Windows.Input.Key.S)
            {
                Save_Click(sender, e);
                e.Handled = true;
            }
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private void UpdateSummary()
        {
            SummaryTxt.Text =
                $"{_preset.Video.Width}x{_preset.Video.Height} • {_preset.Video.Codec} • {_preset.Video.Fps:0.###}fps • " +
                $"{_preset.Video.RateControl} {(_preset.Video.RateControl == RateControl.CRF ? _preset.Video.CRF : _preset.Video.BitrateKbps + " kbps")} • " +
                $"{_preset.Audio.Codec} {_preset.Audio.BitrateKbps}kbps";
        }
    }
}
