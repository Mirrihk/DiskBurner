using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace DiskBurner;

// ========= Models =========

public sealed class TrackInfo
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public int TrackNumber { get; set; }

    public string SourceFile { get; set; } = ""; // temp download
    public string WavFile { get; set; } = "";    // 44.1k/16-bit stereo
    public TimeSpan? Duration { get; set; }
}

public sealed class AlbumProject
{
    public string AlbumTitle { get; set; } = "Untitled Album";
    public string AlbumArtist { get; set; } = "Various Artists";
    public string Genre { get; set; } = "Unknown";
    public string Year { get; set; } = DateTime.Now.Year.ToString();

    public List<TrackInfo> Tracks { get; set; } = new();

    public string OutputDir { get; set; } = "";
    public string CuePath { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string CoverImagePath { get; set; } = "";
}

// ========= Engine =========

public sealed class AlbumEngine
{
    private readonly YoutubeClient _youtube = new();

    public string FfmpegPath { get; set; } = @"C:\ProgramData\chocolatey\bin\ffmpeg.exe";
    public string ImgBurnPath { get; set; } = @"C:\Program Files (x86)\ImgBurn\ImgBurn.exe";

    // WinForms can subscribe to these
    public event Action<string>? LogMessage;
    public event Action<int>? Progress;                 // 0..100 (overall)
    public event Action<int>? TrackProgress;            // 0..100 (current track)
    public event Action<string>? Status;                // short UI status line
    public event Action<TimeSpan>? TotalDurationChanged;

    public async Task<AlbumProject> BuildProjectFromUrlsAsync(
        string albumTitle,
        string albumArtist,
        string genre,
        string year,
        IEnumerable<string> urls,
        string? outputRootDir = null,
        CancellationToken ct = default)
    {
        albumTitle = (albumTitle ?? "").Trim();
        albumArtist = (albumArtist ?? "").Trim();
        genre = (genre ?? "").Trim();
        year = (year ?? "").Trim();

        if (string.IsNullOrWhiteSpace(albumTitle)) albumTitle = "Untitled Album";
        if (string.IsNullOrWhiteSpace(albumArtist)) albumArtist = "Various Artists";
        if (string.IsNullOrWhiteSpace(genre)) genre = "Unknown";
        if (string.IsNullOrWhiteSpace(year)) year = DateTime.Now.Year.ToString();

        var root = outputRootDir ?? Path.Combine(Environment.CurrentDirectory, FileName.Safe(albumTitle));
        Directory.CreateDirectory(root);

        var project = new AlbumProject
        {
            AlbumTitle = albumTitle,
            AlbumArtist = albumArtist,
            Genre = genre,
            Year = year,
            OutputDir = root,
            ProjectPath = Path.Combine(root, "album.project.json"),
            CuePath = Path.Combine(root, FileName.Safe(albumTitle) + ".cue")
        };

        var urlList = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct()
            .ToList();

        Status?.Invoke("Loading YouTube metadata…");
        LogMessage?.Invoke($"Building project from {urlList.Count} URL(s)…");

        int trackNum = 1;
        foreach (var url in urlList)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var video = await _youtube.Videos.GetAsync(url, ct);

                // Heuristic: "Artist - Title"
                var artist = albumArtist;
                var title = video.Title;

                var m = Regex.Match(video.Title, @"^\s*(.+?)\s*-\s*(.+)$");
                if (m.Success)
                {
                    artist = m.Groups[1].Value.Trim();
                    title = m.Groups[2].Value.Trim();
                }

                project.Tracks.Add(new TrackInfo
                {
                    Url = url,
                    Title = title,
                    Artist = artist,
                    TrackNumber = trackNum++
                });

                LogMessage?.Invoke($"Added: {artist} — {title}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[!] Skipping URL: {url} ({ex.Message})");
            }
        }

        Status?.Invoke("Project ready");
        return project;
    }

    /* PSEUDOCODE (detailed plan)
     - Purpose: Fix the compile error when calling YoutubeExplode's DownloadAsync where the method signature expects an optional IProgress<double> before CancellationToken.
     - Approach:
       1. Locate the DownloadAndConvertAllAsync method in AlbumEngine.cs.
       2. Keep all existing logic unchanged except for the download call.
       3. Replace the call:
            await _youtube.Videos.Streams.DownloadAsync(audio, t.SourceFile, ct);
          with a call that uses a named parameter for cancellationToken so the compiler
          does not try to treat the CancellationToken as IProgress<double>:
            await _youtube.Videos.Streams.DownloadAsync(audio, t.SourceFile, cancellationToken: ct);
       4. Preserve the surrounding progress/status updates and exception/cleanup behavior.
       5. Ensure the modified method compiles and maintains the original behavior.

     - Notes:
       - Using the named parameter 'cancellationToken:' ensures the correct overload argument is matched.
       - No other functional changes are made.
    */

    public async Task DownloadAndConvertAllAsync(AlbumProject project, CancellationToken ct = default)
    {
        ValidateToolPaths();

        if (project.Tracks.Count == 0)
        {
            LogMessage?.Invoke("No tracks in project.");
            Progress?.Invoke(0);
            return;
        }

        int total = project.Tracks.Count;
        int done = 0;

        foreach (var t in project.Tracks.OrderBy(x => x.TrackNumber))
        {
            ct.ThrowIfCancellationRequested();

            Status?.Invoke($"Track {t.TrackNumber:D2}: {t.Title}");
            TrackProgress?.Invoke(0);

            LogMessage?.Invoke($"=== Track {t.TrackNumber:D2}: {t.Artist} — {t.Title} ===");

            t.WavFile = string.IsNullOrWhiteSpace(t.WavFile)
                ? Path.Combine(project.OutputDir,
                    $"{t.TrackNumber:D2} - {FileName.Safe(t.Artist)} - {FileName.Safe(t.Title)}.wav")
                : t.WavFile;

            if (File.Exists(t.WavFile))
            {
                LogMessage?.Invoke("WAV already exists, skipping.");
                done++;
                Progress?.Invoke(done * 100 / total);
                continue;
            }

            TrackProgress?.Invoke(5);

            // Stream manifest + best audio-only
            Status?.Invoke($"Fetching stream manifest…");
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(t.Url, ct);
            var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            if (audio is null)
            {
                LogMessage?.Invoke("No audio-only stream found; skipping.");
                done++;
                Progress?.Invoke(done * 100 / total);
                continue;
            }

            // Use extension from stream container (mp4/webm/etc.)
            var ext = audio.Container.Name; // e.g. "mp4" / "webm"
            t.SourceFile = Path.Combine(project.OutputDir, $"{t.TrackNumber:D2}_temp.{ext}");

            // ====== DOWNLOAD (FIXED) ======
            Status?.Invoke("Downloading audio…");
            LogMessage?.Invoke("Downloading audio…");

            TrackProgress?.Invoke(10);

            // Use named parameter for cancellationToken to match the DownloadAsync overload
            await _youtube.Videos.Streams.DownloadAsync(audio, t.SourceFile, cancellationToken: ct);

            TrackProgress?.Invoke(55);
            LogMessage?.Invoke("Download complete.");

            // ====== CONVERT ======
            Status?.Invoke("Converting to WAV…");
            LogMessage?.Invoke("Converting to WAV (44.1kHz, 16-bit, stereo)…");

            var ffArgs = $"-y -i \"{t.SourceFile}\" -ac 2 -ar 44100 -sample_fmt s16 \"{t.WavFile}\"";
            var conv = Proc.Run(FfmpegPath, ffArgs);

            if (!conv.Ok)
            {
                LogMessage?.Invoke("[!] FFmpeg conversion failed:");
                LogMessage?.Invoke(conv.Output);
                SafeDelete(t.SourceFile);
                done++;
                Progress?.Invoke(done * 100 / total);
                TrackProgress?.Invoke(0);
                continue;
            }

            TrackProgress?.Invoke(85);

            // Duration via ffprobe (optional)
            Status?.Invoke("Reading duration…");
            var ffprobe = Path.Combine(Path.GetDirectoryName(FfmpegPath) ?? "", "ffprobe.exe");
            if (File.Exists(ffprobe))
            {
                var probe = Proc.Run(
                    ffprobe,
                    $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{t.WavFile}\"",
                    redirectStdOut: true);

                if (probe.Ok &&
                    double.TryParse(probe.Output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    t.Duration = TimeSpan.FromSeconds(seconds);
                }
            }

            SafeDelete(t.SourceFile);

            TrackProgress?.Invoke(100);
            LogMessage?.Invoke("Done.");

            done++;
            Progress?.Invoke(done * 100 / total);

            // Update total whenever we learn more durations
            RaiseTotalDuration(project);
        }

        Status?.Invoke("All tracks ready");
    }

    public void GenerateCue(AlbumProject project)
    {
        if (string.IsNullOrWhiteSpace(project.CuePath))
            project.CuePath = Path.Combine(project.OutputDir, FileName.Safe(project.AlbumTitle) + ".cue");

        var sb = new StringBuilder();
        sb.AppendLine($"REM GENRE {project.Genre}");
        sb.AppendLine($"REM DATE {project.Year}");
        sb.AppendLine($"PERFORMER \"{Cue.Escape(project.AlbumArtist)}\"");
        sb.AppendLine($"TITLE \"{Cue.Escape(project.AlbumTitle)}\"");

        foreach (var t in project.Tracks.OrderBy(x => x.TrackNumber))
        {
            var fileName = Path.GetFileName(t.WavFile);

            sb.AppendLine($"FILE \"{Cue.Escape(fileName)}\" WAVE");
            sb.AppendLine($"  TRACK {t.TrackNumber:D2} AUDIO");
            sb.AppendLine($"    PERFORMER \"{Cue.Escape(t.Artist)}\"");
            sb.AppendLine($"    TITLE \"{Cue.Escape(t.Title)}\"");
            sb.AppendLine($"    INDEX 01 00:00:00");
        }

        File.WriteAllText(project.CuePath, sb.ToString(), Encoding.UTF8);
        LogMessage?.Invoke($"CUE written: {project.CuePath}");
    }

    public void GenerateCoverHtml(AlbumProject project, string? path = null)
    {
        path ??= Path.Combine(project.OutputDir, "cover.html");

        var imageFile = "";
        if (!string.IsNullOrWhiteSpace(project.CoverImagePath))
            imageFile = Path.GetFileName(project.CoverImagePath);

        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html><head><meta charset='utf-8'>");
        html.AppendLine("<title>Album Cover</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:32px;text-align:center;}");
        html.AppendLine("img{max-width:100%;max-height:300px;margin-bottom:20px;}");
        html.AppendLine("h1{margin:0}");
        html.AppendLine(".meta{color:#555;margin-bottom:20px}");
        html.AppendLine("ol{line-height:1.8;text-align:left;display:inline-block}");
        html.AppendLine("</style>");
        html.AppendLine("</head><body>");

        if (!string.IsNullOrWhiteSpace(imageFile))
            html.AppendLine($"<img src=\"{imageFile}\" alt=\"Album Cover\" />");

        html.AppendLine($"<h1>{Web.EscapeHtml(project.AlbumTitle)}</h1>");
        html.AppendLine($"<div class='meta'>{Web.EscapeHtml(project.AlbumArtist)} • {Web.EscapeHtml(project.Year)} • {Web.EscapeHtml(project.Genre)}</div>");

        html.AppendLine("<ol>");
        foreach (var t in project.Tracks.OrderBy(x => x.TrackNumber))
        {
            var dur = t.Duration.HasValue ? $" — {Fmt.Duration(t.Duration.Value)}" : "";
            html.AppendLine($"<li><strong>{Web.EscapeHtml(t.Title)}</strong> by {Web.EscapeHtml(t.Artist)}{Web.EscapeHtml(dur)}</li>");
        }
        html.AppendLine("</ol>");

        html.AppendLine("</body></html>");
        File.WriteAllText(path, html.ToString(), Encoding.UTF8);

        LogMessage?.Invoke($"Cover HTML created: {path}");
    }

    public async Task SaveProjectAsync(AlbumProject project, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectPath))
            project.ProjectPath = Path.Combine(project.OutputDir, "album.project.json");

        var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(project.ProjectPath, json, Encoding.UTF8, ct);
        LogMessage?.Invoke($"Project saved: {project.ProjectPath}");
    }

    public void LaunchImgBurn(string cuePath, bool verify = false, bool eject = true)
    {
        if (!File.Exists(ImgBurnPath))
        {
            LogMessage?.Invoke("ImgBurn not found. Install it and update ImgBurnPath.");
            return;
        }
        if (!File.Exists(cuePath))
        {
            LogMessage?.Invoke("CUE not found: " + cuePath);
            return;
        }

        var verifyArg = verify ? "/VERIFY YES" : "/VERIFY NO";
        var ejectArg = eject ? "/EJECT YES" : "/EJECT NO";

        var args = $"/MODE WRITE /SRC \"{cuePath}\" /START {verifyArg} {ejectArg}";
        var r = Proc.Run(ImgBurnPath, args);

        LogMessage?.Invoke(r.Ok ? "ImgBurn launched." : $"ImgBurn failed:\n{r.Output}");
    }

    private void ValidateToolPaths()
    {
        if (!File.Exists(FfmpegPath))
            throw new FileNotFoundException("FFmpeg not found at path", FfmpegPath);
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }

    private void RaiseTotalDuration(AlbumProject project)
    {
        // only sums tracks where duration is known
        var totalSeconds = project.Tracks
            .Where(t => t.Duration.HasValue)
            .Sum(t => t.Duration!.Value.TotalSeconds);

        var total = TimeSpan.FromSeconds(totalSeconds);

        TotalDurationChanged?.Invoke(total);

        if (total.TotalMinutes > 80)
            LogMessage?.Invoke($"⚠ WARNING: Total time {Fmt.Duration(total)} exceeds 80:00 CD limit.");
        else
            LogMessage?.Invoke($"Total time: {Fmt.Duration(total)}");
    }
}

// ========= Helpers (small + focused) =========

file static class FileName
{
    public static string Safe(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "untitled";

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = Regex.Replace(name, @"\s+", " ").Trim();
        name = Regex.Replace(name, @"_+", "_").Trim('_');

        return name.Length == 0 ? "untitled" : name;
    }
}

file static class Cue
{
    public static string Escape(string s) => (s ?? "").Replace("\"", "''");
}

file static class Web
{
    public static string EscapeHtml(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}

file static class Fmt
{
    public static string Duration(TimeSpan ts) => $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
}

file static class Proc
{
    public static (bool Ok, string Output) Run(string exe, string args, bool redirectStdOut = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = redirectStdOut,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)!;
            var stderr = p.StandardError.ReadToEnd();
            var stdout = redirectStdOut ? p.StandardOutput.ReadToEnd() : "";
            p.WaitForExit();

            var text = (stderr + "\n" + stdout).Trim();
            return (p.ExitCode == 0, text);
        }
        catch (Exception ex)
        {
            return (false, ex.ToString());
        }
    }
}
