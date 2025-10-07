namespace Fun_Dub_Tool_Box.Utilities.Collections
{
    public enum MaterialType { Intro, Video, Logo, Subtitles, Audio, Outro }

    public sealed class MaterialItem
    {
        public int Index { get; set; }                 // for grid
        public MaterialType Type { get; set; }
        public string Path { get; set; } = "";
        public string Duration { get; set; } = "";        // "00:01:23.450"
        public string Resolution { get; set; } = "";      // "1920x1080@30"
        public string Extra { get; set; } = "";           // audio info etc.
    }

}
