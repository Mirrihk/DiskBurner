using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiskBurner
{
    public sealed class DiskBurnerController
    {
        private readonly AlbumEngine _engine = new();
        private AlbumProject? _project;

        private readonly TextBox _albumTitle;
        private readonly TextBox _albumArtist;
        private readonly TextBox _genre;
        private readonly TextBox _year;
        private readonly TextBox _urls;
        private readonly TextBox _log;
        private readonly ProgressBar _progress;

        private readonly Button _btnBuild;
        private readonly Button _btnDownload;
        private readonly Button _btnCue;
        private readonly Button _btnCover;
        private readonly Button _btnSave;
        private readonly Button _btnBurn;
        private readonly Button _btnOpenFolder;

        public DiskBurnerController(
            TextBox albumTitle, TextBox albumArtist, TextBox genre, TextBox year,
            TextBox urls, TextBox log, ProgressBar progress,
            Button btnBuild, Button btnDownload, Button btnCue, Button btnCover,
            Button btnSave, Button btnBurn, Button btnOpenFolder)
        {
            _albumTitle = albumTitle;
            _albumArtist = albumArtist;
            _genre = genre;
            _year = year;
            _urls = urls;
            _log = log;
            _progress = progress;

            _btnBuild = btnBuild;
            _btnDownload = btnDownload;
            _btnCue = btnCue;
            _btnCover = btnCover;
            _btnSave = btnSave;
            _btnBurn = btnBurn;
            _btnOpenFolder = btnOpenFolder;

            HookEngine();
            SetDefaults();
            SetButtonsEnabled(false);
            _btnBuild.Enabled = true;
        }

        private void HookEngine()
        {
            _engine.Log += UiLog;
            _engine.Progress += UiProgress;
        }

        private void SetDefaults()
        {
            _year.Text = DateTime.Now.Year.ToString();
            _genre.Text = "Unknown";
            _albumArtist.Text = "Various Artists";
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _btnDownload.Enabled = enabled;
            _btnCue.Enabled = enabled;
            _btnCover.Enabled = enabled;
            _btnSave.Enabled = enabled;
            _btnBurn.Enabled = enabled;
            _btnOpenFolder.Enabled = enabled;
        }

        public async Task BuildProjectAsync()
        {
            var title = _albumTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Enter an Album Title.");
                return;
            }

            var urls = _urls.Lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (urls.Length == 0)
            {
                MessageBox.Show("Paste at least one YouTube URL.");
                return;
            }

            UiLog("Building project...");
            _progress.Value = 0;

            ToggleUiBusy(true);
            try
            {
                _project = await _engine.BuildProjectFromUrlsAsync(
                    _albumTitle.Text, _albumArtist.Text, _genre.Text, _year.Text, urls);

                UiLog($"Project ready. Tracks: {_project.Tracks.Count}");
                SetButtonsEnabled(true);
            }
            catch (Exception ex)
            {
                UiLog("[!] Build failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Build failed");
            }
            finally
            {
                ToggleUiBusy(false);
            }
        }

        public async Task DownloadAsync()
        {
            if (_project is null)
            {
                MessageBox.Show("Build the project first.");
                return;
            }

            _progress.Value = 0;
            ToggleUiBusy(true);
            try
            {
                await _engine.DownloadAndConvertAllAsync(_project);
                UiLog("Download + convert complete.");
            }
            catch (Exception ex)
            {
                UiLog("[!] Download/convert failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Download/convert failed");
            }
            finally
            {
                ToggleUiBusy(false);
            }
        }

        public void GenerateCue()
        {
            if (_project is null) { MessageBox.Show("Build the project first."); return; }

            try { _engine.GenerateCue(_project); }
            catch (Exception ex)
            {
                UiLog("[!] CUE failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "CUE failed");
            }
        }

        public void GenerateCover()
        {
            if (_project is null) { MessageBox.Show("Build the project first."); return; }

            try { _engine.GenerateCoverHtml(_project); }
            catch (Exception ex)
            {
                UiLog("[!] Cover HTML failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Cover failed");
            }
        }

        public async Task SaveProjectAsync()
        {
            if (_project is null) { MessageBox.Show("Build the project first."); return; }

            ToggleUiBusy(true);
            try { await _engine.SaveProjectAsync(_project); }
            catch (Exception ex)
            {
                UiLog("[!] Save failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Save failed");
            }
            finally { ToggleUiBusy(false); }
        }

        public void Burn()
        {
            if (_project is null) { MessageBox.Show("Build the project first."); return; }

            try
            {
                if (string.IsNullOrWhiteSpace(_project.CuePath) || !File.Exists(_project.CuePath))
                {
                    UiLog("CUE not found, generating...");
                    _engine.GenerateCue(_project);
                }

                _engine.LaunchImgBurn(_project.CuePath);
            }
            catch (Exception ex)
            {
                UiLog("[!] Burn failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Burn failed");
            }
        }

        public void OpenFolder()
        {
            if (_project is null || string.IsNullOrWhiteSpace(_project.OutputDir) || !Directory.Exists(_project.OutputDir))
            {
                MessageBox.Show("No output folder yet. Build the project first.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = _project.OutputDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                UiLog("[!] Open folder failed: " + ex.Message);
            }
        }

        private void ToggleUiBusy(bool busy)
        {
            _btnBuild.Enabled = !busy;
            _btnDownload.Enabled = !busy && _project != null;
            _btnCue.Enabled = !busy && _project != null;
            _btnCover.Enabled = !busy && _project != null;
            _btnSave.Enabled = !busy && _project != null;
            _btnBurn.Enabled = !busy && _project != null;
            _btnOpenFolder.Enabled = !busy && _project != null;
        }

        private void UiLog(string msg)
        {
            if (!_log.IsHandleCreated) return;
            _log.BeginInvoke(new Action(() => _log.AppendText(msg + Environment.NewLine)));
        }

        private void UiProgress(int p)
        {
            if (!_progress.IsHandleCreated) return;
            _progress.BeginInvoke(new Action(() => _progress.Value = Math.Max(0, Math.Min(100, p))));
        }
    }
}
