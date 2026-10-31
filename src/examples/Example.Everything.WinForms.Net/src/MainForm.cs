// File: MainForm.cs
// Brief: MainForm.
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-07-31

using System;
using System.Diagnostics;
using System.Windows.Forms;
using Lib.Everything;

namespace Example.Everything.WinForms.Net
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            foreach (EverythingSort sort in Enum.GetValues(typeof(EverythingSort)))
            {
                cmbSort.Items.Add(new SortItem(sort));
            }

            cmbSort.SelectedIndex = 0;

            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    DoSearch();
                }
            };
            btnSearch.Click += (s, e) => DoSearch();
            lvResults.DoubleClick += (s, e) => OpenSelected();
            Load += (s, e) =>
            {
                UpdateStatus("就绪。请确认 Everything 已在后台运行。");
                try
                {
                    if (!EverythingClient.IsEverythingRunning())
                    {
                        MessageBox.Show(
                            this,
                            "未检测到 Everything.exe 进程，搜索功能不可用。请先启动 Everything 后重试。",
                            "Everything 未运行",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        UpdateStatus("Everything.exe 未运行");
                    }

                    var version = EverythingClient.GetVersion();
                    if (version.Major != 0)
                    {
                        Text = "Example.Everything.WinForms.Net - SDK " + version.ToString(4);
                        AppendLog("SDK 版本: " + version.ToString(4));
                    }

                    AppendLog("数据库已加载: " + (EverythingClient.IsDBLoaded() ? "是" : "否"));
                }
                catch (Exception ex)
                {
                    AppendLog("初始化检测失败：" + ex);
                }
            };
        }

        private void DoSearch()
        {
            string text = txtSearch.Text;
            var sortItem = cmbSort.SelectedItem as SortItem;

            using (var client = new EverythingClient())
            {
                client.SearchText = text;
                client.MatchCase = chkMatchCase.Checked;
                client.MatchWholeWord = chkWholeWord.Checked;
                client.Regex = chkRegex.Checked;
                client.MatchPath = chkMatchPath.Checked;
                client.Max = (uint)numMax.Value;
                client.Sort = sortItem != null ? sortItem.Sort : EverythingSort.NameAscending;

                lvResults.BeginUpdate();
                lvResults.Items.Clear();
                try
                {
                    var results = client.Query();
                    foreach (var result in results)
                    {
                        lvResults.Items.Add(MakeItem(result));
                    }

                    string message = string.Format(
                        "返回 {0} / {1} 条，错误：{2}，排序：{3}，上限：{4}",
                        client.NumResults,
                        client.TotalResults,
                        client.LastError,
                        client.Sort,
                        client.Max == 0 ? "不限" : client.Max.ToString());
                    UpdateStatus(message);
                    AppendLog("查询完成：" + message);
                }
                catch (Exception ex)
                {
                    UpdateStatus("查询失败：" + ex.Message);
                    AppendLog("查询失败：\r\n" + ex);
                }
                finally
                {
                    lvResults.EndUpdate();
                }
            }
        }

        private static ListViewItem MakeItem(EverythingResult result)
        {
            var item = new ListViewItem(result.FullPath ?? result.FileName ?? string.Empty);
            item.SubItems.Add(result.Size.HasValue ? result.Size.Value.ToString("N0") : "-");
            item.SubItems.Add(result.DateModified.HasValue ? result.DateModified.Value.ToString("yyyy-MM-dd HH:mm") : "-");

            string type;
            if (result.IsFolder)
            {
                type = "文件夹";
            }
            else if (result.IsVolume)
            {
                type = "卷";
            }
            else
            {
                type = result.Extension ?? string.Empty;
            }

            item.SubItems.Add(type);
            item.Tag = result;
            return item;
        }

        private void OpenSelected()
        {
            if (lvResults.SelectedItems.Count == 0)
            {
                return;
            }

            var result = lvResults.SelectedItems[0].Tag as EverythingResult;
            if (result == null || string.IsNullOrEmpty(result.FullPath))
            {
                return;
            }

            try
            {
                if (result.IsFolder)
                {
                    Process.Start("explorer.exe", "\"" + result.FullPath + "\"");
                }
                else
                {
                    Process.Start("explorer.exe", "/select,\"" + result.FullPath + "\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog("打开失败：\r\n" + ex);
            }
        }

        private void UpdateStatus(string text)
        {
            lblStatus.Text = text;
        }

        private void AppendLog(string text)
        {
            txtError.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
        }

        private sealed class SortItem
        {
            public SortItem(EverythingSort sort)
            {
                Sort = sort;
            }

            public EverythingSort Sort { get; }

            public override string ToString()
            {
                return Sort.ToString();
            }
        }
    }
}
