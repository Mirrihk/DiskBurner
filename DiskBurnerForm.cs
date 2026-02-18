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

            var uiFont = new Font("Yu Gothic UI", 9F);
            txtAlbumTitle.Font = uiFont;
            txtAlbumArtist.Font = uiFont;
            txtGenre.Font = uiFont;
            txtYear.Font = uiFont;
            txtUrls.Font = uiFont;
            txtLog.Font = uiFont;
            // (optional) form font too:
            this.Font = uiFont;

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

                if (!EditTracksDialog(project))
                {
                    AppendLog("Edit cancelled. Build aborted.");
                    return;
                }

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
        private void RecalculateTrackNumbers(List<TrackInfo> tracks)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                tracks[i].TrackNumber = i + 1;
            }
        }

        private void DiskBurnerForm_Load(object sender, EventArgs e)
        {

        }
        private bool EditTracksDialog(AlbumProject project)
        {
            using var form = new Form
            {
                Text = "Edit & Arrange Tracks",
                StartPosition = FormStartPosition.CenterParent,
                Width = 1000,
                Height = 550
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            // Columns
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "#",
                DataPropertyName = nameof(TrackInfo.TrackNumber),
                Width = 50,
                ReadOnly = true
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Artist",
                DataPropertyName = nameof(TrackInfo.Artist),
                Width = 300
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Title",
                DataPropertyName = nameof(TrackInfo.Title),
                Width = 500
            });

            var list = project.Tracks.OrderBy(t => t.TrackNumber).ToList();
            var binding = new BindingSource { DataSource = list };
            grid.DataSource = binding;

            // Japanese-safe font
            try { grid.Font = new Font("Yu Gothic UI", 10F); } catch { }

            // Buttons panel
            var panel = new Panel { Dock = DockStyle.Bottom, Height = 50 };

            var btnUp = new Button { Text = "↑ Up", Left = 10, Top = 10, Width = 80 };
            var btnDown = new Button { Text = "↓ Down", Left = 100, Top = 10, Width = 80 };
            var btnOk = new Button { Text = "OK", Left = 780, Top = 10, Width = 90, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 880, Top = 10, Width = 90, DialogResult = DialogResult.Cancel };

            panel.Controls.Add(btnUp);
            panel.Controls.Add(btnDown);
            panel.Controls.Add(btnOk);
            panel.Controls.Add(btnCancel);

            // ===== MOVE UP =====
            btnUp.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) return;

                int index = grid.CurrentRow.Index;
                if (index <= 0) return;

                var item = list[index];
                list.RemoveAt(index);
                list.Insert(index - 1, item);

                RecalculateTrackNumbers(list);

                binding.ResetBindings(false);
                grid.CurrentCell = grid.Rows[index - 1].Cells[1];
            };

            // ===== MOVE DOWN =====
            btnDown.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) return;

                int index = grid.CurrentRow.Index;
                if (index >= list.Count - 1) return;

                var item = list[index];
                list.RemoveAt(index);
                list.Insert(index + 1, item);

                RecalculateTrackNumbers(list);

                binding.ResetBindings(false);
                grid.CurrentCell = grid.Rows[index + 1].Cells[1];
            };

            form.Controls.Add(grid);
            form.Controls.Add(panel);

            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;

            var result = form.ShowDialog(this) == DialogResult.OK;

            if (result)
            {
                // Write reordered list back to project
                project.Tracks = list;
            }

            return result;
        }



    }
}
