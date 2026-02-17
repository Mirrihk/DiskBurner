namespace DiskBurner
{
    partial class DiskBurnerForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TextBox txtAlbumTitle;
        private System.Windows.Forms.TextBox txtAlbumArtist;
        private System.Windows.Forms.TextBox txtGenre;
        private System.Windows.Forms.TextBox txtYear;
        private System.Windows.Forms.TextBox txtUrls;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Button btnBuild;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblArtist;
        private System.Windows.Forms.Label lblGenre;
        private System.Windows.Forms.Label lblYear;
        private System.Windows.Forms.Label lblUrls;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            txtAlbumTitle = new TextBox();
            txtAlbumArtist = new TextBox();
            txtGenre = new TextBox();
            txtYear = new TextBox();
            txtUrls = new TextBox();
            txtLog = new RichTextBox();
            progressBar1 = new ProgressBar();
            btnBuild = new Button();
            lblTitle = new Label();
            lblArtist = new Label();
            lblGenre = new Label();
            lblYear = new Label();
            lblUrls = new Label();
            lblTotalTime = new Label();
            progressTrack = new ProgressBar();
            btnCancel = new Button();
            btnOutputFolder = new Button();
            SuspendLayout();
            // 
            // txtAlbumTitle
            // 
            txtAlbumTitle.Location = new Point(14, 40);
            txtAlbumTitle.Margin = new Padding(3, 4, 3, 4);
            txtAlbumTitle.Name = "txtAlbumTitle";
            txtAlbumTitle.Size = new Size(377, 27);
            txtAlbumTitle.TabIndex = 1;
            txtAlbumTitle.Text = "Untitled Album";
            // 
            // txtAlbumArtist
            // 
            txtAlbumArtist.Location = new Point(14, 104);
            txtAlbumArtist.Margin = new Padding(3, 4, 3, 4);
            txtAlbumArtist.Name = "txtAlbumArtist";
            txtAlbumArtist.Size = new Size(377, 27);
            txtAlbumArtist.TabIndex = 3;
            txtAlbumArtist.Text = "Various Artists";
            // 
            // txtGenre
            // 
            txtGenre.Location = new Point(14, 168);
            txtGenre.Margin = new Padding(3, 4, 3, 4);
            txtGenre.Name = "txtGenre";
            txtGenre.Size = new Size(228, 27);
            txtGenre.TabIndex = 5;
            txtGenre.Text = "Unknown";
            // 
            // txtYear
            // 
            txtYear.Location = new Point(258, 168);
            txtYear.Margin = new Padding(3, 4, 3, 4);
            txtYear.Name = "txtYear";
            txtYear.Size = new Size(132, 27);
            txtYear.TabIndex = 7;
            txtYear.Text = "2026";
            // 
            // txtUrls
            // 
            txtUrls.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtUrls.Location = new Point(14, 235);
            txtUrls.Margin = new Padding(3, 4, 3, 4);
            txtUrls.Multiline = true;
            txtUrls.Name = "txtUrls";
            txtUrls.ScrollBars = ScrollBars.Vertical;
            txtUrls.Size = new Size(377, 185);
            txtUrls.TabIndex = 9;
            // 
            // txtLog
            // 
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtLog.Location = new Point(411, 40);
            txtLog.Margin = new Padding(3, 4, 3, 4);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.Size = new Size(479, 431);
            txtLog.TabIndex = 12;
            txtLog.Text = "";
            // 
            // progressBar1
            // 
            progressBar1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            progressBar1.Location = new Point(151, 435);
            progressBar1.Margin = new Padding(3, 4, 3, 4);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(240, 37);
            progressBar1.TabIndex = 11;
            // 
            // btnBuild
            // 
            btnBuild.Location = new Point(14, 435);
            btnBuild.Margin = new Padding(3, 4, 3, 4);
            btnBuild.Name = "btnBuild";
            btnBuild.Size = new Size(126, 37);
            btnBuild.TabIndex = 10;
            btnBuild.Text = "Build Album";
            btnBuild.UseVisualStyleBackColor = true;
            btnBuild.Click += btnBuild_Click;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(14, 16);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(86, 20);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Album Title";
            // 
            // lblArtist
            // 
            lblArtist.AutoSize = true;
            lblArtist.Location = new Point(14, 80);
            lblArtist.Name = "lblArtist";
            lblArtist.Size = new Size(92, 20);
            lblArtist.TabIndex = 2;
            lblArtist.Text = "Album Artist";
            // 
            // lblGenre
            // 
            lblGenre.AutoSize = true;
            lblGenre.Location = new Point(14, 144);
            lblGenre.Name = "lblGenre";
            lblGenre.Size = new Size(48, 20);
            lblGenre.TabIndex = 4;
            lblGenre.Text = "Genre";
            // 
            // lblYear
            // 
            lblYear.AutoSize = true;
            lblYear.Location = new Point(258, 144);
            lblYear.Name = "lblYear";
            lblYear.Size = new Size(37, 20);
            lblYear.TabIndex = 6;
            lblYear.Text = "Year";
            // 
            // lblUrls
            // 
            lblUrls.AutoSize = true;
            lblUrls.Location = new Point(14, 211);
            lblUrls.Name = "lblUrls";
            lblUrls.Size = new Size(102, 20);
            lblUrls.TabIndex = 8;
            lblUrls.Text = "YouTube URLs";
            // 
            // lblTotalTime
            // 
            lblTotalTime.AutoSize = true;
            lblTotalTime.Location = new Point(411, 9);
            lblTotalTime.Name = "lblTotalTime";
            lblTotalTime.Size = new Size(76, 20);
            lblTotalTime.TabIndex = 13;
            lblTotalTime.Text = "Total: 0:00";
            lblTotalTime.Click += lblTotalTime_Click;
            // 
            // progressTrack
            // 
            progressTrack.Location = new Point(765, 9);
            progressTrack.Name = "progressTrack";
            progressTrack.Size = new Size(125, 29);
            progressTrack.TabIndex = 14;
            progressTrack.Click += progressBar2_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(665, 9);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(94, 29);
            btnCancel.TabIndex = 15;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOutputFolder
            // 
            btnOutputFolder.Location = new Point(565, 9);
            btnOutputFolder.Name = "btnOutputFolder";
            btnOutputFolder.Size = new Size(94, 29);
            btnOutputFolder.TabIndex = 16;
            btnOutputFolder.Text = "Output";
            btnOutputFolder.UseVisualStyleBackColor = true;
            // 
            // DiskBurnerForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(914, 493);
            Controls.Add(btnOutputFolder);
            Controls.Add(btnCancel);
            Controls.Add(progressTrack);
            Controls.Add(lblTotalTime);
            Controls.Add(txtLog);
            Controls.Add(progressBar1);
            Controls.Add(btnBuild);
            Controls.Add(txtUrls);
            Controls.Add(lblUrls);
            Controls.Add(txtYear);
            Controls.Add(lblYear);
            Controls.Add(txtGenre);
            Controls.Add(lblGenre);
            Controls.Add(txtAlbumArtist);
            Controls.Add(lblArtist);
            Controls.Add(txtAlbumTitle);
            Controls.Add(lblTitle);
            Margin = new Padding(3, 4, 3, 4);
            Name = "DiskBurnerForm";
            Text = "DiskBurner";
            ResumeLayout(false);
            PerformLayout();
        }
        private Label lblTotalTime;
        private ProgressBar progressTrack;
        private Button btnCancel;
        private Button btnOutputFolder;
    }
}
