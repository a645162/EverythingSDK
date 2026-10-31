// File: libs/EverythingSDK/src/libs/Lib.Everything.Framework/src/EverythingBackend.cs
// Brief: 托管后端抽象：Windows 走 Everything*.dll，POSIX 走 locate/find/进程内扫描。
// Author: Haomin Kong
// Email: konghm@beim.com.cn / a645162@163.com
// Created: 2026-10-31

using System.Text;

namespace Lib.Everything
{
    /// <summary>
    /// Everything 客户端后端的统一契约。
    /// 方法命名刻意贴近 Everything C SDK，方便与原生版本逐项对照。
    /// </summary>
    internal interface IEverythingBackend
    {
        /// <summary>后端名称，用于诊断信息（"Everything" / "locate" / "find" / "scan"）。</summary>
        string Name { get; }

        /// <summary>本平台可用的后端名称；Windows 只有 "Everything"，POSIX 至少含 "scan"。</summary>
        string[] AvailableBackends { get; }

        /// <summary>手动指定的后端名称；空字符串表示自动（内置优先级）。</summary>
        string PreferredBackend { get; }

        /// <summary>手动指定后端；空字符串表示恢复自动。无法识别的名称会被忽略。</summary>
        void SetPreferredBackend(string name);

        void Reset();
        void CleanUp();

        void SetSearch(string searchText);
        void SetMatchPath(bool enabled);
        void SetMatchCase(bool enabled);
        void SetMatchWholeWord(bool enabled);
        void SetRegex(bool enabled);
        void SetMax(uint value);
        void SetOffset(uint value);
        void SetSort(uint value);
        void SetRequestFlags(uint value);

        bool Query(bool wait);
        uint LastError { get; }
        uint TotalResults { get; }
        uint NumResults { get; }

        bool IsVolumeResult(uint index);
        bool IsFolderResult(uint index);
        bool IsFileResult(uint index);

        string ResultFileName(uint index);
        string ResultPath(uint index);
        string ResultExtension(uint index);
        string ResultFileListFileName(uint index);
        uint ResultFullPathName(uint index, StringBuilder buffer, uint bufferSize);

        uint ResultAttributes(uint index);
        uint ResultRunCount(uint index);
        bool TryResultSize(uint index, out long size);
        bool TryResultDateCreated(uint index, out long fileTime);
        bool TryResultDateModified(uint index, out long fileTime);
        bool TryResultDateAccessed(uint index, out long fileTime);
        bool TryResultDateRun(uint index, out long fileTime);
        bool TryResultDateRecentlyChanged(uint index, out long fileTime);

        uint MajorVersion { get; }
        uint MinorVersion { get; }
        uint Revision { get; }
        uint BuildNumber { get; }
        uint TargetMachine { get; }
        bool IsDBLoaded();

        /// <summary>后端当前是否可用；不可用时通过 reason 返回具体原因。</summary>
        bool IsBackendAvailable(out string reason);
    }
}
