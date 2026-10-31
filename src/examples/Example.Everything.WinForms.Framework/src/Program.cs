// File: Program.cs
// Brief: Program.
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-07-31

using System;
using System.Windows.Forms;

namespace Example.Everything.WinForms.Framework
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
