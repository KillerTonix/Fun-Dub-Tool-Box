using FFMpegCore;
using Fun_Dub_Tool_Box.Utilities.Collections;
using System.Globalization;
using System.IO;

namespace Fun_Dub_Tool_Box.Utilities
{
    /// <summary>
    /// Provides helpers for rendering <see cref="RenderJob"/> instances with FFmpeg through FFMpegCore.
    /// </summary>
    public sealed class FfmpegRenderService
    {
        private sealed class MediaInput
        {
            public MediaInput(
                MaterialItem? material,
                int index,
                bool hasAudio,
                TimeSpan duration,
                string path,
                bool isLogo,
                bool isAudioOnly,
                int width,
                int height)
            {
                Material = material;
                Index = index;
                HasAudio = hasAudio;
                Duration = duration;
                Path = path;
                IsLogo = isLogo;
                IsAudioOnly = isAudioOnly;
                Width = width;
                Height = height;
            }

            public MaterialItem? Material { get; }
            public int Index { get; }
            public bool HasAudio { get; }
            public TimeSpan Duration { get; }
            public string Path { get; }
            public bool IsLogo { get; }
            public bool IsAudioOnly { get; }
            public int Width { get; }
            public int Height { get; }
        }

        public async Task RenderAsync(
            RenderJob job,
            Preset preset,
            IProgress<FFMpegProgress>? progress,
            Action<TimeSpan>? onTimelineDuration,
            CancellationToken cancellationToken)
        {
            if (job is null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (preset is null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            if (string.IsNullOrWhiteSpace(job.OutputPath))
            {
                throw new InvalidOperationException("Render job does not contain a valid output path.");
            }

            var outputDirectory = Path.GetDirectoryName(job.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var orderedMaterials = job.Materials
                .Where(m => m != null)
                .OrderBy(m => m.Index)
                .ToList();

            var timelineMaterials = orderedMaterials
                .Where(m => m.Type is MaterialType.Intro or MaterialType.Video or MaterialType.Outro)
                .ToList();

            if (timelineMaterials.Count == 0)
            {
                throw new InvalidOperationException("Render job must contain at least one video clip.");
            }

            var inputs = new List<MediaInput>();
            foreach (var material in timelineMaterials)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureFileExists(material.Path);
                var analysis = await FFProbe.AnalyseAsync(material.Path).ConfigureAwait(false);
                bool hasAudio = analysis?.PrimaryAudioStream != null || (analysis?.AudioStreams?.Any() ?? false);
                var duration = analysis?.Duration ?? TimeSpan.Zero;
                int width = analysis?.PrimaryVideoStream?.Width ?? 0;
                int height = analysis?.PrimaryVideoStream?.Height ?? 0;
                inputs.Add(new MediaInput(material, inputs.Count, hasAudio, duration, material.Path, isLogo: false, isAudioOnly: false, width: width, height: height));
            }

            var logoMaterial = orderedMaterials.FirstOrDefault(m => m.Type == MaterialType.Logo && File.Exists(m.Path));
            if (logoMaterial != null)
            {
                inputs.Add(new MediaInput(logoMaterial, inputs.Count, hasAudio: false, TimeSpan.Zero, logoMaterial.Path, isLogo: true, isAudioOnly: false, width: 0, height: 0));
            }

            var additionalAudioMaterials = orderedMaterials
                .Where(m => m.Type == MaterialType.Audio && File.Exists(m.Path))
                .ToList();

            foreach (var audio in additionalAudioMaterials)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureFileExists(audio.Path);
                var analysis = await FFProbe.AnalyseAsync(audio.Path).ConfigureAwait(false);
                bool hasAudio = analysis?.PrimaryAudioStream != null || (analysis?.AudioStreams?.Any() ?? false);
                var duration = analysis?.Duration ?? TimeSpan.Zero;
                inputs.Add(new MediaInput(audio, inputs.Count, hasAudio, duration, audio.Path, isLogo: false, isAudioOnly: true, width: 0, height: 0));
            }

            var timelineDuration = inputs
                .Where(i => i.Material != null && i.Material.Type is MaterialType.Intro or MaterialType.Video or MaterialType.Outro)
                .Aggregate(TimeSpan.Zero, (acc, item) => acc + item.Duration);

            if (timelineDuration <= TimeSpan.Zero)
            {
                timelineDuration = TimeSpan.FromSeconds(timelineMaterials.Count);
            }

            onTimelineDuration?.Invoke(timelineDuration);

            var arguments = BuildArgumentPipeline(inputs, job, preset, out string videoLabel, out string? audioLabel, out string? filterComplex);

            var pipeline = arguments.OutputToFile(job.OutputPath, true, options =>
            {
                ConfigureVideoOptions(options, preset.Video);
                ConfigureAudioOptions(options, preset.Audio, !string.IsNullOrWhiteSpace(audioLabel));
                ConfigureGeneralOptions(options, preset.General);

                if (!string.IsNullOrWhiteSpace(filterComplex))
                {
                    options.WithCustomArgument("-filter_complex " + Quote(filterComplex));
                }

                options.WithCustomArgument("-map " + videoLabel);

                if (!string.IsNullOrWhiteSpace(audioLabel))
                {
                    options.WithCustomArgument("-map " + audioLabel);
                }
                else
                {
                    options.WithCustomArgument("-an");
                }
            });

            if (progress != null)
            {
                pipeline = pipeline.NotifyOnProgress(progress, TimeSpan.FromMilliseconds(500));
            }

            await pipeline.ProcessAsynchronously(true, cancellationToken).ConfigureAwait(false);
        }

        private static FFMpegArguments BuildArgumentPipeline(
            List<MediaInput> inputs,
            RenderJob job,
            Preset preset,
            out string videoLabel,
            out string? audioLabel,
            out string? filterComplex)
        {
            if (inputs.Count == 0)
            {
                throw new InvalidOperationException("No inputs available for the FFmpeg command.");
            }

            var arguments = FFMpegArguments.FromFileInput(inputs[0].Path);
            foreach (var input in inputs.Skip(1))
            {
                arguments = arguments.AddFileInput(input.Path, true, opts =>
                {
                    if (input.IsLogo)
                    {
                        opts.WithCustomArgument("-loop 1");
                    }
                });
            }

            filterComplex = BuildFilterComplex(inputs, job, preset, out videoLabel, out audioLabel);
            return arguments;
        }

        private static string? BuildFilterComplex(
            List<MediaInput> inputs,
            RenderJob job,
            Preset preset,
            out string videoLabel,
            out string? audioLabel)
        {
            var videoInputs = inputs
                .Where(i => i.Material != null && i.Material.Type is MaterialType.Intro or MaterialType.Video or MaterialType.Outro)
                .ToList();

            if (videoInputs.Count == 0)
            {
                throw new InvalidOperationException("The render job does not contain any video inputs.");
            }

            var logoInput = inputs.FirstOrDefault(i => i.IsLogo);
            var audioInputs = inputs.Where(i => i.IsAudioOnly).ToList();

            var filterParts = new List<string>();
            int videoFilters = 0;
            int audioFilters = 0;
            int logoFilters = 0;

            static string NextLabel(ref int counter, string prefix) => "[" + prefix + Interlocked.Increment(ref counter) + "]";

            string currentVideoLabel;
            string? currentAudioLabel = null;

            if (videoInputs.Count == 1)
            {
                currentVideoLabel = "[" + videoInputs[0].Index + ":v]";
                if (videoInputs[0].HasAudio)
                {
                    currentAudioLabel = "[" + videoInputs[0].Index + ":a]";
                }
            }
            else
            {
                var videoConcatInputs = string.Join(string.Empty, videoInputs.Select(v => "[" + v.Index + ":v]"));
                var videoConcatLabel = NextLabel(ref videoFilters, "v");
                filterParts.Add(videoConcatInputs + "concat=n=" + videoInputs.Count + ":v=1:a=0" + videoConcatLabel);
                currentVideoLabel = videoConcatLabel;

                if (videoInputs.All(v => v.HasAudio))
                {
                    var audioConcatInputs = string.Join(string.Empty, videoInputs.Select(v => "[" + v.Index + ":a]"));
                    var audioConcatLabel = NextLabel(ref audioFilters, "a");
                    filterParts.Add(audioConcatInputs + "concat=n=" + videoInputs.Count + ":v=0:a=1" + audioConcatLabel);
                    currentAudioLabel = audioConcatLabel;
                }
                else
                {
                    var fallbackAudio = videoInputs.FirstOrDefault(v => v.HasAudio);
                    if (fallbackAudio != null)
                    {
                        currentAudioLabel = "[" + fallbackAudio.Index + ":a]";
                    }
                }
            }

            if (preset.Video.Width > 0 && preset.Video.Height > 0)
            {
                var scaledLabel = NextLabel(ref videoFilters, "v");
                var width = preset.Video.Width.ToString(CultureInfo.InvariantCulture);
                var height = preset.Video.Height.ToString(CultureInfo.InvariantCulture);
                filterParts.Add(currentVideoLabel + "scale=w=" + width + ":h=" + height + ":force_original_aspect_ratio=decrease,pad=" + width + ":" + height + ":(ow-iw)/2:(oh-ih)/2" + scaledLabel);
                currentVideoLabel = scaledLabel;
            }

            var subtitleMaterial = job.Materials.FirstOrDefault(m => m.Type == MaterialType.Subtitles && File.Exists(m.Path));
            if (subtitleMaterial != null)
            {
                var subtitlesLabel = NextLabel(ref videoFilters, "v");
                filterParts.Add(currentVideoLabel + "subtitles='" + EscapeFilterPath(subtitleMaterial.Path) + "'" + subtitlesLabel);
                currentVideoLabel = subtitlesLabel;
            }

            if (logoInput != null)
            {
                var scaleFactor = Math.Clamp(job.Logo.ScalePercent, 1.0, 400.0) / 100.0;
                var formattedScale = scaleFactor.ToString("0.####", CultureInfo.InvariantCulture);
                var rawLogoLabel = "[" + logoInput.Index + ":v]";
                var scaledLogoLabel = NextLabel(ref logoFilters, "l");
                filterParts.Add(rawLogoLabel + "format=rgba,scale=iw*" + formattedScale + ":ih*" + formattedScale + scaledLogoLabel);

                var opacityLabel = NextLabel(ref logoFilters, "l");
                var opacity = Math.Clamp(job.Logo.Opacity, 0, 1).ToString("0.###", CultureInfo.InvariantCulture);
                filterParts.Add(scaledLogoLabel + "format=rgba,colorchannelmixer=aa=" + opacity + opacityLabel);

                var overlayLabel = NextLabel(ref videoFilters, "v");
                var outputWidth = DetermineOutputWidth(preset.Video, videoInputs);
                var outputHeight = DetermineOutputHeight(preset.Video, videoInputs);
                var (overlayX, overlayY) = ResolveLogoPosition(job.Logo, outputWidth, outputHeight);
                filterParts.Add(currentVideoLabel + opacityLabel + "overlay=" + overlayX + ":" + overlayY + ":format=auto" + overlayLabel);
                currentVideoLabel = overlayLabel;
            }

            var audioLabels = new List<string>();
            if (!string.IsNullOrWhiteSpace(currentAudioLabel))
            {
                audioLabels.Add(currentAudioLabel);
            }

            foreach (var audio in audioInputs)
            {
                audioLabels.Add("[" + audio.Index + ":a]");
            }

            if (audioLabels.Count > 1)
            {
                var mixLabel = NextLabel(ref audioFilters, "a");
                filterParts.Add(string.Join(string.Empty, audioLabels) + "amix=inputs=" + audioLabels.Count + ":duration=longest:dropout_transition=2" + mixLabel);
                currentAudioLabel = mixLabel;
            }
            else if (audioLabels.Count == 1)
            {
                currentAudioLabel = audioLabels[0];
            }
            else
            {
                currentAudioLabel = null;
            }

            if (currentAudioLabel != null && preset.Audio.Normalize)
            {
                var loudNormLabel = NextLabel(ref audioFilters, "a");
                filterParts.Add(currentAudioLabel + "loudnorm=I=" + preset.Audio.TargetLufs.ToString("0.##", CultureInfo.InvariantCulture) + ":TP=" + preset.Audio.TruePeakDb.ToString("0.##", CultureInfo.InvariantCulture) + ":LRA=" + preset.Audio.Lra.ToString("0.##", CultureInfo.InvariantCulture) + loudNormLabel);
                currentAudioLabel = loudNormLabel;
            }

            if (filterParts.Count == 0)
            {
                videoLabel = videoInputs[0].Index + ":v:0";
                audioLabel = videoInputs[0].HasAudio ? videoInputs[0].Index + ":a:0" : null;
                return null;
            }

            videoLabel = currentVideoLabel;
            audioLabel = currentAudioLabel;
            return string.Join(';', filterParts);
        }

        private static int DetermineOutputWidth(VideoSettings settings, List<MediaInput> videoInputs)
        {
            if (settings.Width > 0)
            {
                return settings.Width;
            }

            return videoInputs.FirstOrDefault(i => i.Width > 0)?.Width ?? 0;
        }

        private static int DetermineOutputHeight(VideoSettings settings, List<MediaInput> videoInputs)
        {
            if (settings.Height > 0)
            {
                return settings.Height;
            }

            return videoInputs.FirstOrDefault(i => i.Height > 0)?.Height ?? 0;
        }

        private static void ConfigureVideoOptions(FFMpegArgumentOptions options, VideoSettings settings)
        {
            options.WithVideoCodec(SelectVideoCodec(settings));
            options.WithCustomArgument("-pix_fmt " + settings.PixelFormat.ToString());
            options.WithCustomArgument("-r " + settings.Fps.ToString(CultureInfo.InvariantCulture));

            switch (settings.RateControl)
            {
                case RateControl.CRF:
                    options.WithCustomArgument("-crf " + settings.CRF.ToString(CultureInfo.InvariantCulture));
                    break;
                case RateControl.VBR:
                    options.WithCustomArgument("-b:v " + settings.BitrateKbps.ToString(CultureInfo.InvariantCulture) + "k");
                    break;
                case RateControl.CBR:
                    var bitrate = settings.BitrateKbps.ToString(CultureInfo.InvariantCulture);
                    options.WithCustomArgument("-b:v " + bitrate + "k");
                    options.WithCustomArgument("-maxrate " + bitrate + "k");
                    options.WithCustomArgument("-bufsize " + (settings.BitrateKbps * 2).ToString(CultureInfo.InvariantCulture) + "k");
                    break;
            }

            if (!string.Equals(settings.Profile, "auto", StringComparison.OrdinalIgnoreCase))
            {
                options.WithCustomArgument("-profile:v " + settings.Profile);
            }

            if (!string.Equals(settings.Level, "auto", StringComparison.OrdinalIgnoreCase))
            {
                options.WithCustomArgument("-level:v " + settings.Level);
            }
        }

        private static void ConfigureAudioOptions(FFMpegArgumentOptions options, AudioSettings settings, bool includeAudio)
        {
            if (!includeAudio)
            {
                return;
            }

            options.WithAudioCodec(SelectAudioCodec(settings.Codec));
            options.WithCustomArgument("-b:a " + settings.BitrateKbps.ToString(CultureInfo.InvariantCulture) + "k");
            options.WithCustomArgument("-ar " + settings.SampleRate.ToString(CultureInfo.InvariantCulture));
            options.WithCustomArgument("-ac " + settings.Channels.ToString(CultureInfo.InvariantCulture));
        }

        private static void ConfigureGeneralOptions(FFMpegArgumentOptions options, GeneralSettings settings)
        {
            options.ForceFormat(settings.Container.ToString());

            if (settings.FastStart)
            {
                options.WithCustomArgument("-movflags +faststart");
            }

            if (settings.UseColorRangeFull)
            {
                options.WithCustomArgument("-color_range 2");
            }
        }

        private static string SelectVideoCodec(VideoSettings settings)
        {
            return settings.Hardware switch
            {
                HardwareEncoder.NVENC => settings.Codec switch
                {
                    VideoCodec.H265 => "hevc_nvenc",
                    VideoCodec.AV1 => "av1_nvenc",
                    _ => "h264_nvenc"
                },
                HardwareEncoder.QSV => settings.Codec switch
                {
                    VideoCodec.H265 => "hevc_qsv",
                    VideoCodec.AV1 => "av1_qsv",
                    _ => "h264_qsv"
                },
                HardwareEncoder.AMF => settings.Codec switch
                {
                    VideoCodec.H265 => "hevc_amf",
                    _ => "h264_amf"
                },
                _ => settings.Codec switch
                {
                    VideoCodec.H265 => "libx265",
                    VideoCodec.VP9 => "libvpx-vp9",
                    VideoCodec.AV1 => "libaom-av1",
                    _ => "libx264"
                }
            };
        }

        private static string SelectAudioCodec(AudioCodec codec)
        {
            return codec switch
            {
                AudioCodec.Opus => "libopus",
                AudioCodec.PCM_S16LE => "pcm_s16le",
                _ => "aac"
            };
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string EscapeFilterPath(string path)
        {
            return path
                .Replace("\\", "\\\\")
                .Replace("'", "\\'");
        }

        private static void EnsureFileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"Input file '{path}' could not be found.", path);
            }
        }

        private static (string X, string Y) ResolveLogoPosition(LogoSettings settings, int width, int height)
        {
            if (settings.UseManualPlacement)
            {
                return (
                    settings.ManualX.ToString("0.###", CultureInfo.InvariantCulture),
                    settings.ManualY.ToString("0.###", CultureInfo.InvariantCulture));
            }

            return settings.Anchor switch
            {
                LogoAnchor.TopLeft => ("0", "0"),
                LogoAnchor.TopRight => ("(W-w)", "0"),
                LogoAnchor.BottomLeft => ("0", "(H-h)"),
                LogoAnchor.BottomRight => ("(W-w)", "(H-h)"),
                _ => ("0", "0")
            };
        }
    }
}