using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DiskBurner
{
    public partial class DiskBurnerForm : Form
    {
        private AlbumProject? _project;
        private readonly AlbumEngine _engine = new();
        private CancellationTokenSource? _cts;
        private string _outputFolder = "";

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
                value = Math.Clamp(value, 0, 100);
                if (InvokeRequired) Invoke(() => progressBar1.Value = value);
                else progressBar1.Value = value;
            };

            _engine.TrackProgress += value =>
            {
                value = Math.Clamp(value, 0, 100);
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
            btnBurn.Enabled = false;
            progressBar1.Value = 0;
            progressTrack.Value = 0;

            _cts = new CancellationTokenSource();

            try
            {
                var urls = txtUrls.Lines;

                // 1) Build project (metadata + track list)
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

                // 2) Download + convert (creates WAVs + sets WavFile paths)
                await _engine.DownloadAndConvertAllAsync(project, _cts.Token);

                // 3) Let user edit + reorder AFTER WAVs exist
                if (!EditTracksDialog(project))
                {
                    AppendLog("Edit cancelled. Build aborted.");
                    return;
                }

                // 4) (Optional but recommended) rename WAV files to match new order/title/artist
                //    This fixes “Track 01 file is still named 17 - ...” after you reorder.
                RenameWavsToMatchMetadata(project);

                // 5) Regenerate CUE + cover + save AFTER edits
                _engine.GenerateCue(project);
                _engine.GenerateCoverHtml(project);
                await _engine.SaveProjectAsync(project, _cts.Token);

                btnBurn.Enabled = true;

                MessageBox.Show("Album build complete!", "Done");

                if (MessageBox.Show("Burn CD now?", "Burn", MessageBoxButtons.YesNo) == DialogResult.Yes)
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
                MessageBox.Show(ex.Message, "Build failed");
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
                    var dest = Path.Combine(_project.OutputDir, Path.GetFileName(dlg.FileName));
                    File.Copy(dlg.FileName, dest, true);
                    _project.CoverImagePath = dest;

                    AppendLog($"Cover set: {dest}");

                    // Regenerate cover HTML if already built
                    try
                    {
                        _engine.GenerateCoverHtml(_project);
                    }
                    catch { /* ignore */ }
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

            // Always regenerate cue right before burn (keeps burn consistent with last edits)
            try
            {
                // Guard: ensure WAVs exist
                var missing = _project.Tracks.Where(t => string.IsNullOrWhiteSpace(t.WavFile) || !File.Exists(t.WavFile)).ToList();
                if (missing.Count > 0)
                {
                    MessageBox.Show($"WAV file missing for track {missing[0].TrackNumber:D2}:\n{missing[0].WavFile}");
                    return;
                }

                _engine.GenerateCue(_project);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "CUE generation failed");
                return;
            }

            if (string.IsNullOrWhiteSpace(_project.CuePath) || !File.Exists(_project.CuePath))
            {
                MessageBox.Show("CUE file not found.");
                return;
            }

            if (!File.Exists(_engine.ImgBurnPath))
            {
                MessageBox.Show($"ImgBurn not found:\n{_engine.ImgBurnPath}");
                return;
            }

            try
            {
                var args = $"/MODE WRITE /SRC \"{_project.CuePath}\" /START /VERIFY NO /EJECT YES";

                var psi = new ProcessStartInfo
                {
                    FileName = _engine.ImgBurnPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);

                AppendLog($"🔥 Launching ImgBurn with CUE: {_project.CuePath}");
                AppendLog("Tip: In ImgBurn, make sure CD-TEXT writing is enabled for best car display.");
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

        private void DiskBurnerForm_Load(object sender, EventArgs e)
        {
        }

        // ===========================
        // EDIT + ARRANGE TRACKS DIALOG
        // ===========================
        private static void RecalculateTrackNumbers(List<TrackInfo> tracks)
        {
            for (int i = 0; i < tracks.Count; i++)
                tracks[i].TrackNumber = i + 1;
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

            try { grid.Font = new Font("Yu Gothic UI", 10F); } catch { }

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 50 };

            var btnUp = new Button { Text = "↑ Up", Left = 10, Top = 10, Width = 80 };
            var btnDown = new Button { Text = "↓ Down", Left = 100, Top = 10, Width = 80 };
            var btnOk = new Button { Text = "OK", Left = 780, Top = 10, Width = 90, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 880, Top = 10, Width = 90, DialogResult = DialogResult.Cancel };

            panel.Controls.Add(btnUp);
            panel.Controls.Add(btnDown);
            panel.Controls.Add(btnOk);
            panel.Controls.Add(btnCancel);

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

            var ok = form.ShowDialog(this) == DialogResult.OK;

            if (ok)
            {
                // Commit edits + order back to project
                project.Tracks = list;
            }

            return ok;
        }

        // ===========================
        // Rename WAV files to match edited metadata + order
        // ===========================
        private void RenameWavsToMatchMetadata(AlbumProject project)
        {
            foreach (var t in project.Tracks.OrderBy(t => t.TrackNumber))
            {
                if (string.IsNullOrWhiteSpace(t.WavFile) || !File.Exists(t.WavFile))
                    continue;

                var newPath = Path.Combine(project.OutputDir,
                    $"{t.TrackNumber:D2} - {SafeFileName(t.Artist)} - {SafeFileName(t.Title)}.wav");

                if (string.Equals(t.WavFile, newPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    if (File.Exists(newPath))
                        File.Delete(newPath);

                    File.Move(t.WavFile, newPath);
                    AppendLog($"Renamed WAV: {Path.GetFileName(t.WavFile)} → {Path.GetFileName(newPath)}");
                    t.WavFile = newPath;
                }
                catch (Exception ex)
                {
                    // Not fatal; cue will still work if it points to existing file
                    AppendLog($"[!] WAV rename failed for track {t.TrackNumber:D2}: {ex.Message}");
                }
            }
        }

        private static string SafeFileName(string name)
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
}