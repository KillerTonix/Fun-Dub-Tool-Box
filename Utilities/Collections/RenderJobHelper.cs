using Fun_Dub_Tool_Box.Utilities.Collections;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Fun_Dub_Tool_Box.Utilities
{
    public static class RenderJobHelper
    {
        public static string BuildSuggestedOutputName(RenderJob job, Preset preset)
        {
            var pattern = preset.General.FileNamePattern;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return job.Title + job.ContainerExt;
            }

            var title = string.IsNullOrWhiteSpace(job.Title) ? "Project" : job.Title;
            var result = pattern.Replace("{title}", title);

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
    }
}