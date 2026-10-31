// File: EverythingNativePosix.cs
// Brief: POSIX 后端核心：搜索状态、查询流程与结果读取。
// Author: Haomin Kong
// Email: konghm@beim.com.cn / a645162@163.com
// Created: 2026-10-31
//
// 说明：该文件仅在非 .NET Framework 目标中参与编译
// （netstandard2.0 / net6.0 / net8.0 / net10.0）；
// .NET Framework 目标是 Windows 专用，不需要也不应该编译本后端。

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Lib.Everything
{
    /// <summary>
    /// POSIX 版 Everything 后端。
    /// 搜索优先级：plocate/locate 数据库 → fd → find → 进程内目录扫描。
    /// 可通过环境变量调整：
    ///   EVERYTHING_SDK_SEARCH_BACKEND=locate|fd|find|scan
    ///   EVERYTHING_SDK_LOCATE_DB=/path/to/plocate.db
    ///   EVERYTHING_SDK_SEARCH_ROOTS=/a:/b
    ///   EVERYTHING_SDK_MAX_CANDIDATES=200000
    /// </summary>
    internal sealed partial class EverythingNativePosix : IEverythingBackend
    {
        private const int DefaultCandidateCap = 200000;

        private string searchText = string.Empty;
        private bool matchPath;
        private bool matchCase;
        private bool matchWholeWord;
        private bool useRegex;
        private uint max;
        private uint offset;
        private uint sort = (uint)EverythingSort.NameAscending;
        private uint requestFlags;

        private uint lastError;
        private uint totalResults;
        private uint numResults;

        private readonly List<EverythingResult> pageResults = new List<EverythingResult>();
        private readonly List<Entry> pageEntries = new List<Entry>();

        public string Name => ResolveBackend().Name;

        public void Reset()
        {
            searchText = string.Empty;
            matchPath = false;
            matchCase = false;
            matchWholeWord = false;
            useRegex = false;
            max = 0;
            offset = 0;
            sort = (uint)EverythingSort.NameAscending;
            requestFlags = 0;
            lastError = 0;
            totalResults = 0;
            numResults = 0;
            pageResults.Clear();
            pageEntries.Clear();
        }

        public void CleanUp() => Reset();

        public void SetSearch(string text) => searchText = text ?? string.Empty;
        public void SetMatchPath(bool enabled) => matchPath = enabled;
        public void SetMatchCase(bool enabled) => matchCase = enabled;
        public void SetMatchWholeWord(bool enabled) => matchWholeWord = enabled;
        public void SetRegex(bool enabled) => useRegex = enabled;
        public void SetMax(uint value) => max = value;
        public void SetOffset(uint value) => offset = value;
        public void SetSort(uint value) => sort = value;
        public void SetRequestFlags(uint value) => requestFlags = value;

        public uint LastError => lastError;
        public uint TotalResults => totalResults;
        public uint NumResults => numResults;

        public bool Query(bool wait)
        {
            lastError = 0;
            totalResults = 0;
            numResults = 0;
            pageResults.Clear();
            pageEntries.Clear();

            try
            {
                ParsedQuery query = ParseQuery();
                List<string> candidates = CollectCandidates(query, EffectiveCandidateCap());

                var entries = new List<Entry>(candidates.Count);
                foreach (string candidate in candidates)
                {
                    if (!query.Matches(candidate))
                    {
                        continue;
                    }
                    entries.Add(new Entry(candidate));
                }

                EverythingSort sortValue = (EverythingSort)sort;
                if (SortNeedsMetadata(sortValue))
                {
                    foreach (Entry entry in entries)
                    {
                        EnsureMetadata(entry);
                    }
                }

                entries.Sort((left, right) => CompareEntries(left, right, sortValue));

                totalResults = (uint)Math.Min(entries.Count, int.MaxValue);

                int begin = (int)Math.Min(offset, (uint)entries.Count);
                int end = entries.Count;
                if (max > 0)
                {
                    end = (int)Math.Min((ulong)begin + max, (ulong)entries.Count);
                }

                for (int index = begin; index < end; index++)
                {
                    Entry entry = entries[index];
                    EnsureMetadata(entry);
                    pageResults.Add(BuildResult(entry, (uint)pageResults.Count));
                    pageEntries.Add(entry);
                }

                numResults = (uint)pageResults.Count;
                return true;
            }
            catch (Exception)
            {
                lastError = (uint)EverythingError.Ipc;
                return false;
            }
        }

        public bool IsVolumeResult(uint index) => false;
        public bool IsFolderResult(uint index) => TryGetEntry(index)?.IsFolder ?? false;
        public bool IsFileResult(uint index) => TryGetEntry(index)?.IsFile ?? false;

        public string ResultFileName(uint index) => TryGetResult(index)?.FileName;
        public string ResultPath(uint index) => TryGetResult(index)?.Path;
        public string ResultExtension(uint index) => TryGetResult(index)?.Extension;
        public string ResultFileListFileName(uint index) => null;

        public uint ResultFullPathName(uint index, StringBuilder buffer, uint bufferSize)
        {
            EverythingResult result = TryGetResult(index);
            if (result == null || result.FullPath == null)
            {
                return 0;
            }

            uint required = (uint)result.FullPath.Length + 1;
            if (required <= bufferSize && buffer != null)
            {
                buffer.Clear();
                buffer.Append(result.FullPath);
            }
            return required;
        }

        public uint ResultAttributes(uint index) => TryGetEntry(index)?.Attributes ?? 0;
        public uint ResultRunCount(uint index) => 0;

        public bool TryResultSize(uint index, out long size)
        {
            Entry entry = TryGetEntry(index);
            size = entry?.Size ?? 0;
            return entry != null && entry.Exists;
        }

        public bool TryResultDateCreated(uint index, out long fileTime) => TryFileTime(TryGetEntry(index)?.Created, out fileTime);
        public bool TryResultDateModified(uint index, out long fileTime) => TryFileTime(TryGetEntry(index)?.Modified, out fileTime);
        public bool TryResultDateAccessed(uint index, out long fileTime) => TryFileTime(TryGetEntry(index)?.Accessed, out fileTime);
        public bool TryResultDateRun(uint index, out long fileTime) { fileTime = 0; return false; }
        public bool TryResultDateRecentlyChanged(uint index, out long fileTime) { fileTime = 0; return false; }

        public uint MajorVersion => (uint)(typeof(EverythingClient).Assembly.GetName().Version?.Major ?? 0);
        public uint MinorVersion => (uint)(typeof(EverythingClient).Assembly.GetName().Version?.Minor ?? 0);
        public uint Revision => (uint)(typeof(EverythingClient).Assembly.GetName().Version?.Build ?? 0);
        public uint BuildNumber => 0;

        public uint TargetMachine
        {
            get
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X86:
                        return (uint)EverythingTargetMachine.X86;
                    case Architecture.X64:
                        return (uint)EverythingTargetMachine.X64;
                    case Architecture.Arm:
                        return (uint)EverythingTargetMachine.Arm;
                    case Architecture.Arm64:
                        return (uint)EverythingTargetMachine.Arm64;
                    default:
                        return 0;
                }
            }
        }

        public bool IsDBLoaded() => ResolveBackend().DatabaseLoaded;

        public bool IsBackendAvailable(out string reason)
        {
            BackendInfo info = ResolveBackend();
            reason = info.Reason;
            return info.Kind != BackendKind.None;
        }

        private EverythingResult TryGetResult(uint index)
        {
            return index < pageResults.Count ? pageResults[(int)index] : null;
        }

        private Entry TryGetEntry(uint index)
        {
            return index < pageEntries.Count ? pageEntries[(int)index] : null;
        }

        private static bool TryFileTime(DateTime? value, out long fileTime)
        {
            fileTime = 0;
            if (!value.HasValue)
            {
                return false;
            }

            try
            {
                fileTime = value.Value.ToFileTimeUtc();
                return fileTime > 0;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private EverythingResult BuildResult(Entry entry, uint index)
        {
            var result = new EverythingResult
            {
                Index = index,
                FileName = entry.FileName,
                Path = entry.Directory,
                FullPath = entry.Path,
                Extension = entry.Extension,
                Attributes = entry.Attributes,
                IsFile = entry.IsFile,
                IsFolder = entry.IsFolder
            };

            if (entry.Exists)
            {
                result.Size = entry.Size;
            }
            result.DateCreated = entry.Created;
            result.DateModified = entry.Modified;
            result.DateAccessed = entry.Accessed;
            return result;
        }
    }
}

#endif
