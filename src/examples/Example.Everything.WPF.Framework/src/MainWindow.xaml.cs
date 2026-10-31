// File: MainWindow.xaml.cs
// Brief: MainWindow.xaml.
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-07-31

using Lib.Everything;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Example.Everything.WPF.Framework
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SortComboBox.ItemsSource = Enum.GetValues(typeof(EverythingSort));
            SortComboBox.SelectedItem = EverythingSort.NameAscending;

            SearchButton.Click += (_, __) => DoSearch();
            SearchTextBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    DoSearch();
                }
            };
            Loaded += (_, __) => OnLoaded();
        }

        private void OnLoaded()
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
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    UpdateStatus("Everything.exe 未运行");
                }

                var version = EverythingClient.GetVersion();
                if (version.Major != 0)
                {
                    Title = "Example.Everything.WPF.Framework - SDK " + version.ToString(4);
                    AppendLog("SDK 版本: " + version.ToString(4));
                }

                AppendLog("数据库已加载: " + (EverythingClient.IsDBLoaded() ? "是" : "否"));
            }
            catch (Exception ex)
            {
                AppendLog("初始化检测失败：" + ex);
            }
        }

        private void DoSearch()
        {
            using (var client = new EverythingClient())
            {
                client.SearchText = SearchTextBox.Text;
                client.MatchCase = MatchCaseCheckBox.IsChecked == true;
                client.MatchWholeWord = WholeWordCheckBox.IsChecked == true;
                client.Regex = RegexCheckBox.IsChecked == true;
                client.MatchPath = MatchPathCheckBox.IsChecked == true;
                client.Max = ParseMax();
                client.Sort = SortComboBox.SelectedItem is EverythingSort sort
                    ? sort
                    : EverythingSort.NameAscending;

                try
                {
                    var rows = new List<ResultRow>();
                    foreach (var result in client.Query())
                    {
                        rows.Add(ResultRow.FromResult(result));
                    }

                    ResultsGrid.ItemsSource = rows;
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
                    AppendLog("查询失败：" + ex);
                }
            }
        }

        private uint ParseMax()
        {
            return uint.TryParse(MaxTextBox.Text, out var value) ? value : 1000u;
        }

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is ResultRow row && !string.IsNullOrWhiteSpace(row.FullPath))
            {
                try
                {
                    if (row.IsFolder)
                    {
                        Process.Start("explorer.exe", "\"" + row.FullPath + "\"");
                    }
                    else
                    {
                        Process.Start("explorer.exe", "/select,\"" + row.FullPath + "\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "打开失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    AppendLog("打开失败：" + ex);
                }
            }
        }

        private void UpdateStatus(string text)
        {
            StatusTextBlock.Text = text;
        }

        private void AppendLog(string text)
        {
            LogTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        }

        private sealed class ResultRow
        {
            public string Path { get; set; }
            public string Size { get; set; }
            public string Modified { get; set; }
            public string Type { get; set; }
            public string FullPath { get; set; }
            public bool IsFolder { get; set; }

            public static ResultRow FromResult(EverythingResult result)
            {
                return new ResultRow
                {
                    Path = result.FullPath ?? result.FileName ?? string.Empty,
                    Size = result.Size.HasValue ? result.Size.Value.ToString("N0") : "-",
                    Modified = result.DateModified.HasValue ? result.DateModified.Value.ToString("yyyy-MM-dd HH:mm") : "-",
                    Type = result.IsFolder ? "文件夹" : result.IsVolume ? "卷" : result.Extension ?? string.Empty,
                    FullPath = result.FullPath,
                    IsFolder = result.IsFolder
                };
            }
        }
    }
}
