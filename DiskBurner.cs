using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiskBurner
{
    public partial class DiskBurner : Form
    {
        private readonly AlbumEngine _engine = new();
        private AlbumProject? _project;

        // --- UI controls ---
        private TextBox txtAlbumTitle = null!;
        private TextBox txtAlbumArtist = null!;
        private TextBox txtGenre = null!;
        private TextBox txtYear = null!;
        private TextBox txtUrls = null!;
        private TextBox txtLog = null!;
        private ProgressBar progress = null!;
        private Button btnBuild = null!;
        private Button btnDownload = null!;
        private Button btnCue = null!;
        private Button btnCover = null!;
        private Button btnSave = null!;
        private Button btnBurn = null!;
        private Button btnOpenFolder = null!;

        public DiskBurner()
        {
            InitializeComponent();
            BuildUi();

            // Hook engine output -> UI
            _engine.Log += msg => UiLog(msg);
            _engine.Progress += p => UiProgress(p);

            // Sensible defaults
            txtYear.Text = DateTime.Now.Year.ToString();
            txtGenre.Text = "Unknown";
            txtAlbumArtist.Text = "Various Artists";
        }

        private void DiskBurner_Load(object sender, EventArgs e)
        {
            // Optional startup code
        }

        // =========================
        // UI Construction (No Designer Needed)
        // =========================
        private void BuildUi()
        {
            Text = "DiskBurner";
            Width = 1100;
            Height = 780;
            StartPosition = FormStartPosition.CenterScreen;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(12),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            Controls.Add(root);

            // --- Album info group ---
            var grpMeta = new GroupBox { Text = "Album Info", Dock = DockStyle.Fill };
            root.Controls.Add(grpMeta, 0, 0);
            root.SetColumnSpan(grpMeta, 2);

            var meta = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(10)
            };
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            meta.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            meta.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grpMeta.Controls.Add(meta);

            txtAlbumTitle = NewTextBox();
            txtAlbumArtist = NewTextBox();
            txtGenre = NewTextBox();
            txtYear = NewTextBox();

            meta.Controls.Add(new Label { Text = "Album Title", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            meta.Controls.Add(txtAlbumTitle, 1, 0);
            meta.Controls.Add(new Label { Text = "Year", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
            meta.Controls.Add(txtYear, 3, 0);

            meta.Controls.Add(new Label { Text = "Album Artist", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            meta.Controls.Add(txtAlbumArtist, 1, 1);
            meta.Controls.Add(new Label { Text = "Genre", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
            meta.Controls.Add(txtGenre, 3, 1);

            // --- URLs group ---
            var grpUrls = new GroupBox { Text = "YouTube URLs (one per line)", Dock = DockStyle.Fill };
            root.Controls.Add(grpUrls, 0, 1);

            txtUrls = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Consolas", 10),
            };
            grpUrls.Controls.Add(txtUrls);

            // --- Actions group ---
            var grpActions = new GroupBox { Text = "Actions", Dock = DockStyle.Fill };
            root.Controls.Add(grpActions, 1, 1);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10),
                AutoScroll = true
            };
            grpActions.Controls.Add(actions);

            btnBuild = NewButton("1) Build Project (fetch titles)", async (_, __) => await BuildProjectAsync());
            btnDownload = NewButton("2) Download + Convert (WAV)", async (_, __) => await DownloadAsync());
            btnCue = NewButton("3) Generate CUE", (_, __) => GenerateCue());
            btnCover = NewButton("4) Generate Cover HTML", (_, __) => GenerateCover());
            btnSave = NewButton("5) Save Project JSON", async (_, __) => await SaveProjectAsync());
            btnBurn = NewButton("6) Burn with ImgBurn", (_, __) => Burn());
            btnOpenFolder = NewButton("Open Output Folder", (_, __) => OpenFolder());

            actions.Controls.Add(btnBuild);
            actions.Controls.Add(btnDownload);
            actions.Controls.Add(btnCue);
            actions.Controls.Add(btnCover);
            actions.Controls.Add(btnSave);
            actions.Controls.Add(btnBurn);
            actions.Controls.Add(new Label { Height = 8 });
            actions.Controls.Add(btnOpenFolder);

            // --- Log group ---
            var grpLog = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
            root.Controls.Add(grpLog, 0, 2);
            root.SetColumnSpan(grpLog, 2);

            var logLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10)
            };
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grpLog.Controls.Add(logLayout);

            progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Consolas", 10),
            };

            logLayout.Controls.Add(progress, 0, 0);
            logLayout.Controls.Add(txtLog, 0, 1);

            // start state
            SetButtonsEnabled(false);
            btnBuild.Enabled = true;
        }

        private static TextBox NewTextBox()
            => new TextBox { Dock = DockStyle.Fill };

        private static Button NewButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                Width = 320,
                Height = 40
            };
            b.Click += onClick;
            return b;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            btnDownload.Enabled = enabled;
            btnCue.Enabled = enabled;
            btnCover.Enabled = enabled;
            btnSave.Enabled = enabled;
            btnBurn.Enabled = enabled;
            btnOpenFolder.Enabled = enabled;
        }

        // =========================
        // Actions
        // =========================

        private async Task BuildProjectAsync()
        {
            var title = txtAlbumTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Enter an Album Title.");
                return;
            }

            var urls = txtUrls.Lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (urls.Length == 0)
            {
                MessageBox.Show("Paste at least one YouTube URL.");
                return;
            }

            UiLog("Building project...");
            progress.Value = 0;

            ToggleUiBusy(true);
            try
            {
                _project = await _engine.BuildProjectFromUrlsAsync(
                    txtAlbumTitle.Text,
                    txtAlbumArtist.Text,
                    txtGenre.Text,
                    txtYear.Text,
                    urls);

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

        private async Task DownloadAsync()
        {
            if (_project is null)
            {
                MessageBox.Show("Build the project first.");
                return;
            }

            progress.Value = 0;
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

        private void GenerateCue()
        {
            if (_project is null)
            {
                MessageBox.Show("Build the project first.");
                return;
            }

            try
            {
                _engine.GenerateCue(_project);
            }
            catch (Exception ex)
            {
                UiLog("[!] CUE failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "CUE failed");
            }
        }

        private void GenerateCover()
        {
            if (_project is null)
            {
                MessageBox.Show("Build the project first.");
                return;
            }

            try
            {
                _engine.GenerateCoverHtml(_project);
            }
            catch (Exception ex)
            {
                UiLog("[!] Cover HTML failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Cover failed");
            }
        }

        private async Task SaveProjectAsync()
        {
            if (_project is null)
            {
                MessageBox.Show("Build the project first.");
                return;
            }

            ToggleUiBusy(true);
            try
            {
                await _engine.SaveProjectAsync(_project);
            }
            catch (Exception ex)
            {
                UiLog("[!] Save failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Save failed");
            }
            finally
            {
                ToggleUiBusy(false);
            }
        }

        private void Burn()
        {
            if (_project is null)
            {
                MessageBox.Show("Build the project first.");
                return;
            }

            try
            {
                // Ensure cue exists
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

        private void OpenFolder()
        {
            if (_project is null || string.IsNullOrWhiteSpace(_project.OutputDir) || !Directory.Exists(_project.OutputDir))
            {
                MessageBox.Show("No output folder yet. Build the project first.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _project.OutputDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UiLog("[!] Open folder failed: " + ex.Message);
            }
        }

        // =========================
        // UI helpers
        // =========================

        private void ToggleUiBusy(bool busy)
        {
            btnBuild.Enabled = !busy;
            btnDownload.Enabled = !busy && _project != null;
            btnCue.Enabled = !busy && _project != null;
            btnCover.Enabled = !busy && _project != null;
            btnSave.Enabled = !busy && _project != null;
            btnBurn.Enabled = !busy && _project != null;
            btnOpenFolder.Enabled = !busy && _project != null;

            UseWaitCursor = busy;
        }

        private void UiLog(string msg)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    txtLog.AppendText(msg + Environment.NewLine);
                }));
            }
        }

        private void UiProgress(int p)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    progress.Value = Math.Max(0, Math.Min(100, p));
                }));
            }
        }
    }
}
