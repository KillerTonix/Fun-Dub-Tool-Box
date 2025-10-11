namespace Fun_Dub_Tool_Box.Utilities.Collections
{
    public class RenderJob
    {
        public string Title { get; set; } = "Project";     // from main video base name
        public string OutputFolder { get; set; } = "";     // absolute path
        public string OutputPath { get; set; } = "";       // full path including extension
        public string ContainerExt { get; set; } = ".mp4"; // from preset container
                                                           // ... other fields you already use (preset, inputs, status, etc.)
    }

}
