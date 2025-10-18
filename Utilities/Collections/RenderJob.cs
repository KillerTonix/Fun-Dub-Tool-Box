using System.Collections.Generic;

namespace Fun_Dub_Tool_Box.Utilities.Collections
{
    public class RenderJob
    {
        public int SequenceId { get; set; }
        public string Title { get; set; } = "Project";     // from main video base name
        public string OutputFolder { get; set; } = "";     // absolute path
        public string OutputPath { get; set; } = "";       // full path including extension
        public string ContainerExt { get; set; } = ".mp4"; // from preset container
        public string PresetName { get; set; } = string.Empty;
        public string MainVideoPath { get; set; } = string.Empty;
        public List<MaterialItem> Materials { get; set; } = [];
        public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
        public bool ShutdownWhenCompleted { get; set; }
        public LogoSettings Logo { get; set; } = new();

        public bool GpuAcceleration { get; set; } = true;
    }
}