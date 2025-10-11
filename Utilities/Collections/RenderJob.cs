namespace Fun_Dub_Tool_Box.Utilities.Collections
{
    public class RenderJob
    {
        public int SequenceId { get; set; } = 0; // for queue ordering
        public string Title { get; set; } = "Project";     // from main video base name
        public string OutputFolder { get; set; } = "";     // absolute path
        public string PresetName { get; set; } = "";     // absolute path
        public string MainVideoPath { get; set; } = "";     // absolute path
        public bool ShutdownWhenCompleted { get; set; } = false; // user option
        public List<MaterialItem> Materials { get; set; } = [];     // absolute path
        public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

        public string OutputPath { get; set; } = "";       // full path including extension
        public string ContainerExt { get; set; } = ".mp4"; // from preset container
                                                           // ... other fields you already use (preset, inputs, status, etc.)
    }

}
