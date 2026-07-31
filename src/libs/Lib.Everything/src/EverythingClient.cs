using System;
using System.Collections.Generic;
using System.Text;

namespace Lib.Everything
{
    /// <summary>
    /// voidtools Everything SDK 的高层封装。
    /// 用法：构造后设置 SearchText/MatchCase 等属性，调用 Query() 取回结果；
    /// 也可直接用静态 Search(searchText) 一步完成。需要 Everything 在后台运行。
    /// 非线程安全，请单线程使用或在调用方加锁。
    /// </summary>
    public sealed class EverythingClient : IDisposable
    {
        private const int DefaultBufferSize = 260;

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

        /// <summary>返回结果上限，0 表示不限。对应 Everything_SetMax。</summary>
        public uint Max { get; set; }

        /// <summary>结果偏移，用于分页。对应 Everything_SetOffset。</summary>
        public uint Offset { get; set; }

        /// <summary>排序方式，默认按名称升序。对应 Everything_SetSort。</summary>
        public EverythingSort Sort { get; set; } = EverythingSort.NameAscending;

        /// <summary>请求的结果字段，默认完整路径与文件名。对应 Everything_SetRequestFlags。</summary>
        public EverythingRequestFlags RequestFlags { get; set; } =
            EverythingRequestFlags.FullPathAndFileName |
            EverythingRequestFlags.Path |
            EverythingRequestFlags.Extension |
            EverythingRequestFlags.Size |
            EverythingRequestFlags.DateModified |
            EverythingRequestFlags.DateCreated;

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
            EverythingNative.Everything_Reset();

            EverythingNative.Everything_SetSearchW(SearchText ?? string.Empty);
            EverythingNative.Everything_SetMatchPath(MatchPath);
            EverythingNative.Everything_SetMatchCase(MatchCase);
            EverythingNative.Everything_SetMatchWholeWord(MatchWholeWord);
            EverythingNative.Everything_SetRegex(Regex);
            EverythingNative.Everything_SetMax(Max);
            EverythingNative.Everything_SetOffset(Offset);
            EverythingNative.Everything_SetSort((uint)Sort);
            EverythingNative.Everything_SetRequestFlags((uint)RequestFlags);

            if (!EverythingNative.Everything_QueryW(true))
            {
                LastError = (EverythingError)EverythingNative.Everything_GetLastError();
                throw new InvalidOperationException(
                    "Everything 查询失败：" + LastError + "（请确认 Everything 已在后台运行）。", null);
            }

            LastError = EverythingError.Ok;
            TotalResults = EverythingNative.Everything_GetTotResults();
            NumResults = EverythingNative.Everything_GetNumResults();

            var results = new List<EverythingResult>((int)Math.Min(NumResults, 8192u));
            for (uint i = 0; i < NumResults; i++)
                results.Add(ReadResult(i));
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

        private EverythingResult ReadResult(uint index)
        {
            var r = new EverythingResult { Index = index };

            r.FileName = EverythingNative.PtrToStringUni(EverythingNative.Everything_GetResultFileNameW(index));
            r.Path = EverythingNative.PtrToStringUni(EverythingNative.Everything_GetResultPathW(index));
            r.FullPath = GetFullPath(index);
            r.Extension = EverythingNative.PtrToStringUni(EverythingNative.Everything_GetResultExtensionW(index));
            r.FileListFileName = EverythingNative.PtrToStringUni(EverythingNative.Everything_GetResultFileListFileNameW(index));

            if ((RequestFlags & EverythingRequestFlags.Size) != 0 &&
                EverythingNative.Everything_GetResultSize(index, out long size))
            {
                r.Size = size;
            }

            r.Attributes = EverythingNative.Everything_GetResultAttributes(index);
            r.RunCount = EverythingNative.Everything_GetResultRunCount(index);
            r.IsVolume = EverythingNative.Everything_IsVolumeResult(index);
            r.IsFolder = EverythingNative.Everything_IsFolderResult(index);
            r.IsFile = EverythingNative.Everything_IsFileResult(index);

            if ((RequestFlags & EverythingRequestFlags.DateCreated) != 0 &&
                EverythingNative.Everything_GetResultDateCreated(index, out long created))
            {
                r.DateCreated = SafeFileTime(created);
            }

            if ((RequestFlags & EverythingRequestFlags.DateModified) != 0 &&
                EverythingNative.Everything_GetResultDateModified(index, out long modified))
            {
                r.DateModified = SafeFileTime(modified);
            }

            if ((RequestFlags & EverythingRequestFlags.DateAccessed) != 0 &&
                EverythingNative.Everything_GetResultDateAccessed(index, out long accessed))
            {
                r.DateAccessed = SafeFileTime(accessed);
            }

            if ((RequestFlags & EverythingRequestFlags.DateRun) != 0 &&
                EverythingNative.Everything_GetResultDateRun(index, out long run))
            {
                r.DateRun = SafeFileTime(run);
            }

            if ((RequestFlags & EverythingRequestFlags.DateRecentlyChanged) != 0 &&
                EverythingNative.Everything_GetResultDateRecentlyChanged(index, out long changed))
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

        private static string GetFullPath(uint index)
        {
            // 先用固定缓冲区取，不足时按实际需要的字符数扩容重取。
            var buf = new StringBuilder(DefaultBufferSize);
            uint needed = EverythingNative.Everything_GetResultFullPathNameW(index, buf, (uint)buf.Capacity);
            if (needed > buf.Capacity)
            {
                buf = new StringBuilder((int)needed);
                EverythingNative.Everything_GetResultFullPathNameW(index, buf, (uint)buf.Capacity);
            }
            return buf.ToString();
        }

        /// <summary>Everything SDK 版本号，如 1.4.1.1005。</summary>
        public static Version GetVersion()
        {
            return new Version(
                (int)EverythingNative.Everything_GetMajorVersion(),
                (int)EverythingNative.Everything_GetMinorVersion(),
                (int)EverythingNative.Everything_GetRevision(),
                (int)EverythingNative.Everything_GetBuildNumber());
        }

        /// <summary>目标机器类型，由当前加载的 Everything 原生库决定。</summary>
        public static EverythingTargetMachine GetTargetMachine()
        {
            return (EverythingTargetMachine)EverythingNative.Everything_GetTargetMachine();
        }

        /// <summary>是否已加载 Everything 索引数据库。</summary>
        public static bool IsDBLoaded()
        {
            return EverythingNative.Everything_IsDBLoaded();
        }

        /// <summary>
        /// 检测 Everything.exe 进程是否正在运行。
        /// 比 IsDBLoaded 更可靠，不依赖 SDK 初始化状态。
        /// </summary>
        public static bool IsEverythingRunning()
        {
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

        public void Dispose()
        {
            // 每次 Query 前已 Reset，这里仅释放 SDK 全局状态。
            EverythingNative.Everything_CleanUp();
        }
    }
}
