// File: EverythingResult.cs
// Brief: EverythingResult.
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-07-31

using System;

namespace Lib.Everything
{
    /// <summary>单条搜索结果，路径与文件名总是可用。</summary>
    public sealed class EverythingResult
    {
        public uint Index { get; set; }
        public string FileName { get; set; }
        public string Path { get; set; }
        public string FullPath { get; set; }
        public string Extension { get; set; }
        public long? Size { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public DateTime? DateAccessed { get; set; }
        public uint Attributes { get; set; }
        public string FileListFileName { get; set; }
        public uint RunCount { get; set; }
        public DateTime? DateRun { get; set; }
        public DateTime? DateRecentlyChanged { get; set; }
        public bool IsVolume { get; set; }
        public bool IsFolder { get; set; }
        public bool IsFile { get; set; }

        public override string ToString()
        {
            return FullPath ?? FileName ?? ("Result " + Index);
        }
    }
}
