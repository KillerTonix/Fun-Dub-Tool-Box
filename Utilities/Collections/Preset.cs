// Models/Preset.cs
namespace Fun_Dub_Tool_Box.Utilities.Collections
{
    public enum VideoCodec { H264, H265, VP9, AV1 }
    public enum RateControl { CRF, VBR, CBR }
    public enum HardwareEncoder { Auto, CPU, NVENC, QSV, AMF }
    public enum PixelFormat { yuv420p, yuv422p, yuv444p }
    public enum Container { mp4, mkv, mov }
    public enum AudioCodec { AAC, Opus, PCM_S16LE }

    public sealed class Preset
    {
        public string Name { get; set; } = "Preset 1";
        public VideoSettings Video { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public GeneralSettings General { get; set; } = new();
    }

    public sealed class VideoSettings
    {
        public VideoCodec Codec { get; set; } = VideoCodec.H265;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public double Fps { get; set; } = 30;
        public PixelFormat PixelFormat { get; set; } = PixelFormat.yuv420p;

        public RateControl RateControl { get; set; } = RateControl.CRF;
        public int CRF { get; set; } = 20;           // when CRF
        public int BitrateKbps { get; set; } = 8000; // when VBR/CBR
        public bool TwoPass { get; set; } = false;

        public string Profile { get; set; } = "auto";
        public string Level { get; set; } = "auto";
        public HardwareEncoder Hardware { get; set; } = HardwareEncoder.Auto;
    }

    public sealed class AudioSettings
    {
        public AudioCodec Codec { get; set; } = AudioCodec.AAC;
        public int BitrateKbps { get; set; } = 320;
        public int SampleRate { get; set; } = 48000;
        public int Channels { get; set; } = 2;

        public bool Normalize { get; set; } = true;
        public double TargetLufs { get; set; } = -14;
        public double TruePeakDb { get; set; } = -1.5;
        public double Lra { get; set; } = 11;
    }

    public sealed class GeneralSettings
    {
        public Container Container { get; set; } = Container.mp4;
        public bool FastStart { get; set; } = true; // -movflags +faststart
        public string FileNamePattern { get; set; } = "{title}_{date:yyyyMMdd_HHmm}.mp4";
        public bool UseColorRangeFull { get; set; } = false;
    }
}