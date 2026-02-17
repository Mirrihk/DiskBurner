using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiskBurner
{
    public sealed class DiskBurner : Form
    {
        // ===== Win32 (drag window when borderless) =====
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();

        private void InitializeComponent()
        {

        }

        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // ===== Theme =====
        private static readonly Color Bg = Color.Black;
        private static readonly Color PanelBg = Color.FromArgb(18, 18, 18);
        private static readonly Color Border = Color.FromArgb(60, 60, 60);
        private static readonly Color TextCol = Color.Gainsboro;

        // ===== Engine =====
        private readonly AlbumEngine _engine = new();
        private AlbumProject? _project;

        // ===== UI =====
        private Panel _titleBar = null!;
        private Label _title = null!;
        private Button _minBtn = null!;
        private Button _closeBtn = null!;

        private TextBox _albumTitle = null!;
        private TextBox _albumArtist = null!;
        private TextBox _genre = null!;
        private TextBox _year = null!;
        private TextBox _urls = null!;
        private TextBox _log = null!;
        private ProgressBar _progress = null!;

        private Button _btnBuild = null!;
        private Button _btnDownload = null!;
        private Button _btnCue = null!;
        private Button _btnCover = null!;
        private Button _btnSave = null!;
        private Button _btnBurn = null!;
        private Button _btnOpenFolder = null!;

        public DiskBurner()
        {
            // Borderless so what you see is what you run
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(1);
            BackColor = Bg;
            ForeColor = TextCol;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1100;
            Height = 780;
            Text = "DiskBurner";

            BuildUi();
            HookEngine();
            SetDefaults();
            SetButtonsEnabled(false);
            _btnBuild.Enabled = true;
        }

        // =========================
        // UI
        // =========================
        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(12),
                BackColor = Bg
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));     // title bar
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));    // meta
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));      // urls/actions
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));      // log

            Controls.Add(root);

            // ----- Title bar -----
            _titleBar = new Panel { Dock = DockStyle.Fill, Height = 44, BackColor = PanelBg };
            _titleBar.MouseDown += TitleBar_MouseDown;

            _title = new Label
            {
                Text = "DiskBurner",
                AutoSize = true,
                ForeColor = TextCol,
                Location = new Point(12, 13)
            };
            _title.MouseDown += TitleBar_MouseDown;

            _minBtn = TitleButton("–", BtnMin_Click);
            _closeBtn = TitleButton("X", BtnClose_Click);

            _titleBar.Controls.Add(_title);
            _titleBar.Controls.Add(_minBtn);
            _titleBar.Controls.Add(_closeBtn);
            _titleBar.Resize += (_, __) =>
            {
                _closeBtn.Location = new Point(_titleBar.Width - _closeBtn.Width, 0);
                _minBtn.Location = new Point(_titleBar.Width - _closeBtn.Width - _minBtn.Width, 0);
            };

            root.Controls.Add(_titleBar, 0, 0);
            root.SetColumnSpan(_titleBar, 2);

            // ----- Meta section -----
            var metaGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(12),
                BackColor = PanelBg
            };

            metaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            metaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            metaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            metaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            metaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            metaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            _albumTitle = NewTextBox();
            _albumArtist = NewTextBox();
            _genre = NewTextBox();
            _year = NewTextBox();

            metaGrid.Controls.Add(NewLabel("Album Title"), 0, 0);
            metaGrid.Controls.Add(_albumTitle, 1, 0);
            metaGrid.Controls.Add(NewLabel("Year"), 2, 0);
            metaGrid.Controls.Add(_year, 3, 0);

            metaGrid.Controls.Add(NewLabel("Album Artist"), 0, 1);
            metaGrid.Controls.Add(_albumArtist, 1, 1);
            metaGrid.Controls.Add(NewLabel("Genre"), 2, 1);
            metaGrid.Controls.Add(_genre, 3, 1);

            root.Controls.Add(Section("Album Info", metaGrid), 0, 1);
            root.SetColumnSpan(root.GetControlFromPosition(0, 1), 2);

            // ----- URLs -----
            _urls = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(12, 12, 12),
                ForeColor = TextCol,
                BorderStyle = BorderStyle.FixedSingle
            };
            root.Controls.Add(Section("YouTube URLs (one per line)", _urls), 0, 2);

            // ----- Actions -----
            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = PanelBg
            };

            _btnBuild = NewButton("1) Build Project (fetch titles)", async (_, __) => await BuildProjectAsync());
            _btnDownload = NewButton("2) Download + Convert (WAV)", async (_, __) => await DownloadAsync());
            _btnCue = NewButton("3) Generate CUE", (_, __) => GenerateCue());
            _btnCover = NewButton("4) Generate Cover HTML", (_, __) => GenerateCover());
            _btnSave = NewButton("5) Save Project JSON", async (_, __) => await SaveProjectAsync());
            _btnBurn = NewButton("6) Burn with ImgBurn", (_, __) => Burn());
            _btnOpenFolder = NewButton("Open Output Folder", (_, __) => OpenFolder());

            actionsPanel.Controls.Add(_btnBuild);
            actionsPanel.Controls.Add(_btnDownload);
            actionsPanel.Controls.Add(_btnCue);
            actionsPanel.Controls.Add(_btnCover);
            actionsPanel.Controls.Add(_btnSave);
            actionsPanel.Controls.Add(_btnBurn);
            actionsPanel.Controls.Add(new Label { Height = 8 });
            actionsPanel.Controls.Add(_btnOpenFolder);

            root.Controls.Add(Section("Actions", actionsPanel), 1, 2);

            // ----- Log -----
            var logGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12),
                BackColor = PanelBg
            };
            logGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            logGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(12, 12, 12),
                ForeColor = TextCol,
                BorderStyle = BorderStyle.FixedSingle
            };

            logGrid.Controls.Add(_progress, 0, 0);
            logGrid.Controls.Add(_log, 0, 1);

            root.Controls.Add(Section("Log", logGrid), 0, 3);
            root.SetColumnSpan(root.GetControlFromPosition(0, 3), 2);
        }

        private Control Section(string title, Control content)
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = PanelBg, Padding = new Padding(10) };
            host.Paint += (_, e) =>
            {
                using var p = new Pen(Border);
                e.Graphics.DrawRectangle(p, 0, 0, host.Width - 1, host.Height - 1);
            };

            var lbl = new Label
            {
                Text = title,
                ForeColor = TextCol,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(2, 0, 0, 8)
            };

            content.Dock = DockStyle.Fill;
            host.Controls.Add(content);
            host.Controls.Add(lbl);
            return host;
        }

        private static Label NewLabel(string text)
            => new Label { Text = text, AutoSize = true, ForeColor = TextCol, Anchor = AnchorStyles.Left };

        private static TextBox NewTextBox()
            => new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 12),
                ForeColor = TextCol,
                BorderStyle = BorderStyle.FixedSingle
            };

        private static Button NewButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                Width = 330,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = TextCol
            };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.Click += onClick;
            return b;
        }

        private static Button TitleButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                Width = 44,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = PanelBg,
                ForeColor = TextCol,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += onClick;
            return b;
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

        // =========================
        // Actions
        // =========================
        private async Task BuildProjectAsync()
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
                    _albumTitle.Text,
                    _albumArtist.Text,
                    _genre.Text,
                    _year.Text,
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

        private void GenerateCue()
        {
            if (_project is null) { MessageBox.Show("Build the project first."); return; }

            try { _engine.GenerateCue(_project); }
            catch (Exception ex)
            {
                UiLog("[!] CUE failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "CUE failed");
            }
        }

        private void GenerateCover()
        {
            if (_project is null) { MessageBox.Show("Build the project first."); return; }

            try { _engine.GenerateCoverHtml(_project); }
            catch (Exception ex)
            {
                UiLog("[!] Cover HTML failed: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Cover failed");
            }
        }

        private async Task SaveProjectAsync()
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

        private void Burn()
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
            _btnBuild.Enabled = !busy;
            _btnDownload.Enabled = !busy && _project != null;
            _btnCue.Enabled = !busy && _project != null;
            _btnCover.Enabled = !busy && _project != null;
            _btnSave.Enabled = !busy && _project != null;
            _btnBurn.Enabled = !busy && _project != null;
            _btnOpenFolder.Enabled = !busy && _project != null;

            UseWaitCursor = busy;
        }

        private void UiLog(string msg)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(new Action(() => _log.AppendText(msg + Environment.NewLine)));
        }

        private void UiProgress(int p)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                _progress.Value = Math.Max(0, Math.Min(100, p));
            }));
        }

        // =========================
        // Title bar actions
        // =========================
        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(this.Handle, 0x112, 0xF012, 0);
        }

        private void BtnClose_Click(object? sender, EventArgs e) => Close();
        private void BtnMin_Click(object? sender, EventArgs e) => WindowState = FormWindowState.Minimized;
    }
}
