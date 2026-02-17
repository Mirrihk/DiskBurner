using Microsoft.VisualBasic.Logging;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace DiskBurner
{
    public partial class DiskBurnerForm : Form
    {
        private readonly AlbumEngine _engine = new();

        public DiskBurnerForm()
        {
            InitializeComponent();

            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            txtLog.BackColor = Color.Black;
            txtLog.ForeColor = Color.Lime;

            txtUrls.BackColor = Color.FromArgb(45, 45, 45);
            txtUrls.ForeColor = Color.White;

            btnBuild.BackColor = Color.FromArgb(70, 70, 70);
            btnBuild.ForeColor = Color.White;
            btnBuild.FlatStyle = FlatStyle.Flat;

            _engine.Log += msg =>
            {
                if (InvokeRequired)
                {
                    Invoke(() => AppendLog(msg));
                }
                else
                {
                    AppendLog(msg);
                }
            };

            _engine.Progress += value =>
            {
                if (InvokeRequired)
                {
                    Invoke(() => progressBar1.Value = value);
                }
                else
                {
                    progressBar1.Value = value;
                }
            };
        }

        private void AppendLog(string msg)
        {
            txtLog.AppendText(msg + Environment.NewLine);
        }

        private async void btnBuild_Click(object sender, EventArgs e)
        {
            btnBuild.Enabled = false;

            try
            {
                var urls = txtUrls.Lines;

                var project = await _engine.BuildProjectFromUrlsAsync(
                    txtAlbumTitle.Text,
                    txtAlbumArtist.Text,
                    txtGenre.Text,
                    txtYear.Text,
                    urls
                );

                await _engine.DownloadAndConvertAllAsync(project);
                var totalDuration = project.Tracks
                .Where(x => x.Duration.HasValue)
                .Sum(x => x.Duration.Value.TotalMinutes);

                if (totalDuration > 80)
                {
                    Log?.Invoke("⚠ WARNING: Album exceeds 80 minute CD limit!");
                }

                _engine.GenerateCue(project);
                _engine.GenerateCoverHtml(project);
                await _engine.SaveProjectAsync(project);

                MessageBox.Show("Album build complete!", "Done");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                btnBuild.Enabled = true;
            }
        }

        private void lblTotalTime_Click(object sender, EventArgs e)
        {

        }

        private void progressBar2_Click(object sender, EventArgs e)
        {

        }
    }
}
