using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Diagnostics;

namespace DiskBurner
{
    public partial class DiskBurnerForm : Form
    {
        private AlbumProject? _project;
        private readonly AlbumEngine _engine = new();
        private CancellationTokenSource? _cts;
        private string _outputFolder = "";

        private const string ImgBurnPath =
            @"C:\Program Files (x86)\ImgBurn\ImgBurn.exe";

        public DiskBurnerForm()
        {
            InitializeComponent();

            // Dark theme
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            txtLog.BackColor = Color.Black;
            txtLog.ForeColor = Color.Lime;

            txtUrls.BackColor = Color.FromArgb(45, 45, 45);
            txtUrls.ForeColor = Color.White;

            btnBuild.BackColor = Color.FromArgb(70, 70, 70);
            btnBuild.ForeColor = Color.White;
            btnBuild.FlatStyle = FlatStyle.Flat;

            // ===== ENGINE EVENTS =====

            _engine.LogMessage += msg =>
            {
                if (InvokeRequired) Invoke(() => AppendLog(msg));
                else AppendLog(msg);
            };

            _engine.Progress += value =>
            {
                if (InvokeRequired) Invoke(() => progressBar1.Value = value);
                else progressBar1.Value = value;
            };

            _engine.TrackProgress += value =>
            {
                if (InvokeRequired) Invoke(() => progressTrack.Value = value);
                else progressTrack.Value = value;
            };

            _engine.Status += s =>
            {
                if (InvokeRequired) Invoke(() => Text = $"DiskBurner — {s}");
                else Text = $"DiskBurner — {s}";
            };

            _engine.TotalDurationChanged += total =>
            {
                var text = $"Total: {(int)total.TotalMinutes}:{total.Seconds:D2}";
                if (InvokeRequired) Invoke(() => lblTotalTime.Text = text);
                else lblTotalTime.Text = text;
            };
        }

        private void AppendLog(string msg)
        {
            txtLog.AppendText(msg + Environment.NewLine);
        }

        // ===========================
        // BUILD
        // ===========================
        private async void btnBuild_Click(object sender, EventArgs e)
        {
            btnBuild.Enabled = false;
            progressBar1.Value = 0;
            progressTrack.Value = 0;

            _cts = new CancellationTokenSource();

            try
            {
                var urls = txtUrls.Lines;

                var project = await _engine.BuildProjectFromUrlsAsync(
                    txtAlbumTitle.Text,
                    txtAlbumArtist.Text,
                    txtGenre.Text,
                    txtYear.Text,
                    urls,
                    string.IsNullOrWhiteSpace(_outputFolder) ? null : _outputFolder,
                    _cts.Token
                );

                _project = project;

                await _engine.DownloadAndConvertAllAsync(project, _cts.Token);

                _engine.GenerateCue(project);
                _engine.GenerateCoverHtml(project);
                await _engine.SaveProjectAsync(project);

                MessageBox.Show("Album build complete!", "Done");

                if (MessageBox.Show("Burn CD now?", "Burn",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    btnBurn_Click(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("Operation cancelled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                btnBuild.Enabled = true;
                _cts = null;
            }
        }

        // ===========================
        // CANCEL
        // ===========================
        private void btnCancel_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        // ===========================
        // OUTPUT FOLDER
        // ===========================
        private void btnOutputFolder_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select output folder for album files"
            };

            if (!string.IsNullOrWhiteSpace(_outputFolder))
                dlg.SelectedPath = _outputFolder;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _outputFolder = dlg.SelectedPath;
                AppendLog($"Output folder set to: {_outputFolder}");
            }
        }

        // ===========================
        // COVER IMAGE
        // ===========================
        private void BrowseCover_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select Album Cover",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                using var temp = Image.FromFile(dlg.FileName);
                picCover.Image = new Bitmap(temp);

                if (_project != null)
                {
                    var dest = Path.Combine(_project.OutputDir,
                        Path.GetFileName(dlg.FileName));

                    File.Copy(dlg.FileName, dest, true);
                    _project.CoverImagePath = dest;

                    AppendLog($"Cover set: {dest}");
                }
                else
                {
                    AppendLog("Cover selected (build album first to attach it).");
                }
            }
        }

        // ===========================
        // BURN CD
        // ===========================
        private void btnBurn_Click(object sender, EventArgs e)
        {
            if (_project == null)
            {
                MessageBox.Show("Build an album first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_project.CuePath) ||
                !File.Exists(_project.CuePath))
            {
                MessageBox.Show("CUE file not found.");
                return;
            }

            if (!File.Exists(ImgBurnPath))
            {
                MessageBox.Show($"ImgBurn not found:\n{ImgBurnPath}");
                return;
            }

            try
            {
                var args =
                    $"/MODE WRITE /SRC \"{_project.CuePath}\" /START /VERIFY NO /EJECT YES";

                var psi = new ProcessStartInfo
                {
                    FileName = ImgBurnPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);

                AppendLog($"🔥 Launching ImgBurn with CUE: {_project.CuePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Burn failed");
            }
        }

        // ===========================
        // INFO POPUPS
        // ===========================
        private void lblTotalTime_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                lblTotalTime.Text + Environment.NewLine +
                "Standard CD limit: 80:00",
                "Album Duration Info",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void progressBar2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                $"Current Track Progress: {progressTrack.Value}%",
                "Track Progress",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
