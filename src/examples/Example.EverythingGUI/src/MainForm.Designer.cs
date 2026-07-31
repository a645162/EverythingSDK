namespace Example.EverythingGUI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.CheckBox chkMatchCase;
        private System.Windows.Forms.CheckBox chkWholeWord;
        private System.Windows.Forms.CheckBox chkRegex;
        private System.Windows.Forms.CheckBox chkMatchPath;
        private System.Windows.Forms.Label lblSort;
        private System.Windows.Forms.ComboBox cmbSort;
        private System.Windows.Forms.Label lblMax;
        private System.Windows.Forms.NumericUpDown numMax;
        private System.Windows.Forms.ListView lvResults;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ColumnHeader colPath;
        private System.Windows.Forms.ColumnHeader colSize;
        private System.Windows.Forms.ColumnHeader colModified;
        private System.Windows.Forms.ColumnHeader colType;
        private System.Windows.Forms.TextBox txtError;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.topPanel = new System.Windows.Forms.Panel();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.btnSearch = new System.Windows.Forms.Button();
            this.chkMatchCase = new System.Windows.Forms.CheckBox();
            this.chkWholeWord = new System.Windows.Forms.CheckBox();
            this.chkRegex = new System.Windows.Forms.CheckBox();
            this.chkMatchPath = new System.Windows.Forms.CheckBox();
            this.lblSort = new System.Windows.Forms.Label();
            this.cmbSort = new System.Windows.Forms.ComboBox();
            this.lblMax = new System.Windows.Forms.Label();
            this.numMax = new System.Windows.Forms.NumericUpDown();
            this.lvResults = new System.Windows.Forms.ListView();
            this.colPath = new System.Windows.Forms.ColumnHeader();
            this.colSize = new System.Windows.Forms.ColumnHeader();
            this.colModified = new System.Windows.Forms.ColumnHeader();
            this.colType = new System.Windows.Forms.ColumnHeader();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.txtError = new System.Windows.Forms.TextBox();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMax)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // topPanel
            //
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Padding = new System.Windows.Forms.Padding(8, 8, 8, 4);
            this.topPanel.Height = 84;
            this.topPanel.Controls.Add(this.lblSearch);
            this.topPanel.Controls.Add(this.txtSearch);
            this.topPanel.Controls.Add(this.btnSearch);
            this.topPanel.Controls.Add(this.chkMatchCase);
            this.topPanel.Controls.Add(this.chkWholeWord);
            this.topPanel.Controls.Add(this.chkRegex);
            this.topPanel.Controls.Add(this.chkMatchPath);
            this.topPanel.Controls.Add(this.lblSort);
            this.topPanel.Controls.Add(this.cmbSort);
            this.topPanel.Controls.Add(this.lblMax);
            this.topPanel.Controls.Add(this.numMax);
            //
            // lblSearch
            //
            this.lblSearch.Text = "搜索";
            this.lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblSearch.Location = new System.Drawing.Point(8, 12);
            this.lblSearch.Size = new System.Drawing.Size(40, 24);
            //
            // txtSearch
            //
            this.txtSearch.Location = new System.Drawing.Point(52, 10);
            this.txtSearch.Size = new System.Drawing.Size(360, 24);
            //
            // btnSearch
            //
            this.btnSearch.Text = "搜索";
            this.btnSearch.Location = new System.Drawing.Point(420, 9);
            this.btnSearch.Size = new System.Drawing.Size(80, 26);
            //
            // chkMatchCase
            //
            this.chkMatchCase.Text = "区分大小写";
            this.chkMatchCase.Location = new System.Drawing.Point(8, 44);
            this.chkMatchCase.Size = new System.Drawing.Size(100, 24);
            //
            // chkWholeWord
            //
            this.chkWholeWord.Text = "全字匹配";
            this.chkWholeWord.Location = new System.Drawing.Point(112, 44);
            this.chkWholeWord.Size = new System.Drawing.Size(92, 24);
            //
            // chkRegex
            //
            this.chkRegex.Text = "正则";
            this.chkRegex.Location = new System.Drawing.Point(208, 44);
            this.chkRegex.Size = new System.Drawing.Size(64, 24);
            //
            // chkMatchPath
            //
            this.chkMatchPath.Text = "匹配路径";
            this.chkMatchPath.Location = new System.Drawing.Point(276, 44);
            this.chkMatchPath.Size = new System.Drawing.Size(92, 24);
            //
            // lblSort
            //
            this.lblSort.Text = "排序";
            this.lblSort.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblSort.Location = new System.Drawing.Point(372, 44);
            this.lblSort.Size = new System.Drawing.Size(40, 24);
            //
            // cmbSort
            //
            this.cmbSort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSort.Location = new System.Drawing.Point(416, 42);
            this.cmbSort.Size = new System.Drawing.Size(180, 24);
            //
            // lblMax
            //
            this.lblMax.Text = "上限";
            this.lblMax.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblMax.Location = new System.Drawing.Point(600, 44);
            this.lblMax.Size = new System.Drawing.Size(40, 24);
            //
            // numMax
            //
            this.numMax.Location = new System.Drawing.Point(644, 42);
            this.numMax.Size = new System.Drawing.Size(84, 24);
            this.numMax.Minimum = 0;
            this.numMax.Maximum = 1000000;
            this.numMax.Value = 1000;
            //
            // lvResults
            //
            this.lvResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvResults.View = System.Windows.Forms.View.Details;
            this.lvResults.FullRowSelect = true;
            this.lvResults.GridLines = true;
            this.lvResults.Columns.Add(this.colPath);
            this.lvResults.Columns.Add(this.colSize);
            this.lvResults.Columns.Add(this.colModified);
            this.lvResults.Columns.Add(this.colType);
            //
            // colPath
            //
            this.colPath.Text = "路径";
            this.colPath.Width = 520;
            //
            // colSize
            //
            this.colSize.Text = "大小";
            this.colSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colSize.Width = 110;
            //
            // colModified
            //
            this.colModified.Text = "修改时间";
            this.colModified.Width = 150;
            //
            // colType
            //
            this.colType.Text = "类型";
            this.colType.Width = 80;
            //
            // statusStrip
            //
            this.statusStrip.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusStrip.SizingGrip = false;
            this.statusStrip.Items.Add(this.lblStatus);
            //
            // lblStatus
            //
            this.lblStatus.Spring = true;
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtError
            //
            this.txtError.Multiline = true;
            this.txtError.ReadOnly = true;
            this.txtError.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtError.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtError.Height = 110;
            this.txtError.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtError.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
            this.txtError.Text = "（错误与日志会显示在这里，可整段复制）";
            //
            // MainForm
            //
            this.ClientSize = new System.Drawing.Size(960, 600);
            this.MinimumSize = new System.Drawing.Size(640, 400);
            this.Controls.Add(this.lvResults);
            this.Controls.Add(this.txtError);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.topPanel);
            this.Text = "Example.EverythingGUI";
            this.topPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numMax)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
