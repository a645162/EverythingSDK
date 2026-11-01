// File: EverythingClient.cs
// Brief: EverythingClient.
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-07-31

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Lib.Everything
{
    /// <summary>
    /// voidtools Everything SDK 的高层封装。
    /// 用法：构造后设置 SearchText/MatchCase 等属性，调用 Query() 取回结果；
    /// 也可直接用静态 Search(searchText) 一步完成。
    /// Windows 需要 Everything 在后台运行；POSIX 使用 locate/find/进程内扫描。
    /// 非线程安全，请单线程使用或在调用方加锁。
    /// </summary>
    public sealed class EverythingClient : IDisposable
    {
        private const int DefaultBufferSize = 260;

        /// <summary>长路径（&gt; MAX_PATH）二次获取用的缓冲区长度，与 Windows 长路径上限同量级。</summary>
        private const int ExtendedPathBufferSize = 32768;

        /// <summary>搜索关键字，空串匹配全部。对应 Everything_SetSearchW。</summary>
        public string SearchText { get; set; }

        /// <summary>匹配路径，默认 false。对应 Everything_SetMatchPath。</summary>
        public bool MatchPath { get; set; }

        /// <summary>区分大小写，默认 false。对应 Everything_SetMatchCase。</summary>
        public bool MatchCase { get; set; }

        /// <summary>全字匹配，默认 false。对应 Everything_SetMatchWholeWord。</summary>
        public bool MatchWholeWord { get; set; }

        /// <summary>启用正则，默认 false。对应 Everything_SetRegex。</summary>
        public bool Regex { get; set; }

        /// <summary>
        /// 返回结果上限，0 表示由后端决定上限。对应 Everything_SetMax。
        /// Windows 上 Everything 的 IPC 会把 0 当成“返回 0 条”，因此 0 实际按
        /// 安全上限 200000 下发（与 POSIX 后端的候选上限一致），避免一次拉取
        /// 百万级结果把 Everything 进程撑爆；显式传入更大的值不受影响。
        /// </summary>
        public uint Max { get; set; }

        /// <summary>结果偏移，用于分页。对应 Everything_SetOffset。</summary>
        public uint Offset { get; set; }

        /// <summary>排序方式，默认按名称升序。对应 Everything_SetSort。</summary>
        public EverythingSort Sort { get; set; } = EverythingSort.NameAscending;

        /// <summary>请求的结果字段，默认完整路径与文件名。对应 Everything_SetRequestFlags。</summary>
        public EverythingRequestFlags RequestFlags { get; set; } =
            EverythingRequestFlags.FileName |
            EverythingRequestFlags.FullPathAndFileName |
            EverythingRequestFlags.Path |
            EverythingRequestFlags.Extension |
            EverythingRequestFlags.Size |
            EverythingRequestFlags.DateModified |
            EverythingRequestFlags.DateCreated |
            // Attributes 不请求时 Everything 会回 0xFFFFFFFF（INVALID_FILE_ATTRIBUTES），
            // 调用方没法区分“没属性”和“没请求”，所以默认一起取回来。
            EverythingRequestFlags.Attributes;

        /// <summary>最近一次查询返回的错误码。查询失败后可用它定位原因。</summary>
        public EverythingError LastError { get; private set; }

        /// <summary>当前已查询到的结果总数（含未取回的部分）。对应 Everything_GetTotResults。</summary>
        public uint TotalResults { get; private set; }

        /// <summary>当前已查询到的可见结果数，受 Max/Offset 影响。对应 Everything_GetNumResults。</summary>
        public uint NumResults { get; private set; }

        /// <summary>
        /// 执行一次同步（阻塞）查询并把可见结果读回。
        /// 失败抛 InvalidOperationException，详情见 LastError。
        /// </summary>
        public List<EverythingResult> Query()
        {
            IEverythingBackend backend = EverythingNative.Backend;
            backend.Reset();

            backend.SetSearch(SearchText ?? string.Empty);
            backend.SetMatchPath(MatchPath);
            backend.SetMatchCase(MatchCase);
            backend.SetMatchWholeWord(MatchWholeWord);
            backend.SetRegex(Regex);
            backend.SetMax(Max);
            backend.SetOffset(Offset);
            backend.SetSort((uint)Sort);
            backend.SetRequestFlags((uint)RequestFlags);

            if (!backend.Query(true))
            {
                LastError = (EverythingError)backend.LastError;
                throw new InvalidOperationException(
                    "Everything 查询失败：" + LastError + "（" + DescribeBackend() + "）。", null);
            }

            LastError = EverythingError.Ok;
            TotalResults = backend.TotalResults;
            NumResults = backend.NumResults;

            var results = new List<EverythingResult>((int)Math.Min(NumResults, 8192u));
            for (uint i = 0; i < NumResults; i++)
                results.Add(ReadResult(backend, i));
            return results;
        }

        /// <summary>一步搜索：设置关键字并查询，返回结果列表。</summary>
        public static List<EverythingResult> Search(string searchText)
        {
            using (var client = new EverythingClient())
            {
                client.SearchText = searchText;
                return client.Query();
            }
        }

        private EverythingResult ReadResult(IEverythingBackend backend, uint index)
        {
            var r = new EverythingResult { Index = index };

            r.FileName = backend.ResultFileName(index);
            r.Path = backend.ResultPath(index);
            r.FullPath = GetFullPath(backend, index);
            r.Extension = backend.ResultExtension(index);
            r.FileListFileName = backend.ResultFileListFileName(index);

            if ((RequestFlags & EverythingRequestFlags.Size) != 0 &&
                backend.TryResultSize(index, out long size))
            {
                r.Size = size;
            }

            r.Attributes = backend.ResultAttributes(index);
            r.RunCount = backend.ResultRunCount(index);
            r.IsVolume = backend.IsVolumeResult(index);
            r.IsFolder = backend.IsFolderResult(index);
            r.IsFile = backend.IsFileResult(index);

            if ((RequestFlags & EverythingRequestFlags.DateCreated) != 0 &&
                backend.TryResultDateCreated(index, out long created))
            {
                r.DateCreated = SafeFileTime(created);
            }

            if ((RequestFlags & EverythingRequestFlags.DateModified) != 0 &&
                backend.TryResultDateModified(index, out long modified))
            {
                r.DateModified = SafeFileTime(modified);
            }

            if ((RequestFlags & EverythingRequestFlags.DateAccessed) != 0 &&
                backend.TryResultDateAccessed(index, out long accessed))
            {
                r.DateAccessed = SafeFileTime(accessed);
            }

            if ((RequestFlags & EverythingRequestFlags.DateRun) != 0 &&
                backend.TryResultDateRun(index, out long run))
            {
                r.DateRun = SafeFileTime(run);
            }

            if ((RequestFlags & EverythingRequestFlags.DateRecentlyChanged) != 0 &&
                backend.TryResultDateRecentlyChanged(index, out long changed))
            {
                r.DateRecentlyChanged = SafeFileTime(changed);
            }

            return r;
        }

        /// <summary>
        /// 把 FILETIME（100ns 计数，自 1601）安全转成 DateTime。
        /// SDK 对卷、未索引项等可能返回 0 或越界值，DateTime.FromFileTime 会抛
        /// ArgumentOutOfRangeException，这里越界一律返回 null。
        /// </summary>
        private static DateTime? SafeFileTime(long fileTime)
        {
            if (fileTime < 0) return null;
            try { return DateTime.FromFileTime(fileTime); }
            catch { return null; }
        }

        private static string GetFullPath(IEverythingBackend backend, uint index)
        {
            // 两个后端返回值的语义不一样：
            // POSIX 返回“含结尾 0 的总长度”，缓冲区不足时干脆不填；
            // Windows 的 Everything_GetResultFullPathNameW 返回“实际写入的字符数”，
            // 缓冲区不足时静默截断，不会告诉调用方真实长度。
            // 因此统一判据：结果顶到缓冲区上限（或缓冲区根本没被填）就换长路径缓冲区重取一次。
            var buf = new StringBuilder(DefaultBufferSize);
            int capacity = buf.Capacity;
            uint written = backend.ResultFullPathName(index, buf, (uint)capacity);
            if (written == 0)
            {
                return string.Empty;
            }

            if (written >= capacity - 1 || buf.Length == 0)
            {
                buf = new StringBuilder(ExtendedPathBufferSize);
                backend.ResultFullPathName(index, buf, (uint)buf.Capacity);
            }

            return buf.ToString();
        }

        /// <summary>Everything SDK 版本号，如 1.4.1.1005；POSIX 后端返回本 SDK 版本。</summary>
        public static Version GetVersion()
        {
            IEverythingBackend backend = EverythingNative.Backend;
            return new Version(
                (int)backend.MajorVersion,
                (int)backend.MinorVersion,
                (int)backend.Revision,
                (int)backend.BuildNumber);
        }

        /// <summary>目标机器类型，由当前加载的 Everything 原生库或运行平台决定。</summary>
        public static EverythingTargetMachine GetTargetMachine()
        {
            return (EverythingTargetMachine)EverythingNative.Backend.TargetMachine;
        }

        /// <summary>是否已加载 Everything 索引数据库。POSIX 上表示 locate 数据库是否存在。</summary>
        public static bool IsDBLoaded()
        {
            return EverythingNative.Backend.IsDBLoaded();
        }

        /// <summary>实际使用的后端名称：Windows 为 "Everything"，POSIX 为 "locate"/"fd"/"find"/"scan"。</summary>
        public static string GetBackendName()
        {
            return EverythingNative.Backend.Name;
        }

        /// <summary>本平台可用的后端名称；Windows 只有 "Everything"，POSIX 至少含 "scan"。</summary>
        public static string[] AvailableBackends()
        {
            return EverythingNative.Backend.AvailableBackends;
        }

        /// <summary>手动指定后端；空字符串表示自动。无法识别的名称会被忽略。</summary>
        public static void SetPreferredBackend(string name)
        {
            EverythingNative.Backend.SetPreferredBackend(name);
        }

        /// <summary>当前手动指定的后端；空字符串表示自动。</summary>
        public static string GetPreferredBackend()
        {
            return EverythingNative.Backend.PreferredBackend;
        }

        /// <summary>
        /// 检测搜索后端是否可用。
        /// Windows 上检查 Everything.exe 进程；POSIX 上检查 locate 数据库或 find 可执行文件。
        /// </summary>
        public static bool IsEverythingRunning()
        {
#if !NETFRAMEWORK
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return EverythingNative.Backend.IsBackendAvailable(out _);
            }
#endif
            try
            {
                var procs = System.Diagnostics.Process.GetProcessesByName("Everything");
                return procs != null && procs.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string DescribeBackend()
        {
            try
            {
                return "当前后端：" + EverythingNative.Backend.Name;
            }
            catch
            {
                return "后端不可用";
            }
        }

        public void Dispose()
        {
            // 每次 Query 前已 Reset，这里仅释放后端持有的临时状态。
            EverythingNative.Backend.CleanUp();
        }
    }
}
