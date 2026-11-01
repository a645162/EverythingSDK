// File: libs/EverythingSDK/src/libs/Lib.Everything.Framework/src/EverythingNativeWindows.cs
// Brief: Windows 后端：动态加载 Everything*.dll 并绑定 Everything C SDK 导出函数。
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-10-31

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Lib.Everything
{
    /// <summary>
    /// voidtools Everything SDK 的原生互操作层。
    /// 运行时会根据当前进程 ABI 选择对应的 Everything*.dll。
    /// </summary>
    internal sealed class EverythingNativeWindows : IEverythingBackend
    {
        private static readonly Lazy<NativeExports> nativeExports =
            new Lazy<NativeExports>(LoadNativeExports, true);

        /// <summary>
        /// Max=0（默认值）时下发给 Everything 的安全上限。
        /// Everything 1.4 的 IPC 把 SetMax(0) 当成“返回 0 条”，而“完全不调 SetMax”才是
        /// 真·不限——空搜索会一次性吐出 500 多万行，实测能把 Everything 进程撑到 OOM。
        /// 这里与 POSIX 后端 EVERYTHING_SDK_MAX_CANDIDATES 的 200000 对齐，
        /// 让两端默认查询量级一致；调用方显式传入更大的值时仍然按原值下发。
        /// </summary>
        private const uint DefaultMaxResults = 200000;

        public string Name => "Everything";

        // Windows 上只有 Everything 一个后端，没有可切换的项。
        public string[] AvailableBackends => new[] { "Everything" };

        public string PreferredBackend => "Everything";

        public void SetPreferredBackend(string name)
        {
            // 唯一后端没有「切换到别处」的语义，参数刻意忽略。
        }

        public void Reset() => Native.Everything_Reset();
        public void CleanUp() => Native.Everything_CleanUp();

        public void SetSearch(string searchText) => Native.Everything_SetSearchW(searchText ?? string.Empty);
        public void SetMatchPath(bool enabled) => Native.Everything_SetMatchPath(enabled);
        public void SetMatchCase(bool enabled) => Native.Everything_SetMatchCase(enabled);
        public void SetMatchWholeWord(bool enabled) => Native.Everything_SetMatchWholeWord(enabled);
        public void SetRegex(bool enabled) => Native.Everything_SetRegex(enabled);
        public void SetMax(uint value) => Native.Everything_SetMax(value == 0 ? DefaultMaxResults : value);
        public void SetOffset(uint value) => Native.Everything_SetOffset(value);
        public void SetSort(uint value) => Native.Everything_SetSort(value);
        public void SetRequestFlags(uint value) => Native.Everything_SetRequestFlags(value);

        public bool Query(bool wait) => Native.Everything_QueryW(wait);
        public uint LastError => Native.Everything_GetLastError();
        public uint TotalResults => Native.Everything_GetTotResults();
        public uint NumResults => Native.Everything_GetNumResults();

        public bool IsVolumeResult(uint index) => Native.Everything_IsVolumeResult(index);
        public bool IsFolderResult(uint index) => Native.Everything_IsFolderResult(index);
        public bool IsFileResult(uint index) => Native.Everything_IsFileResult(index);

        public string ResultFileName(uint index) => EverythingNative.PtrToStringUni(Native.Everything_GetResultFileNameW(index));
        public string ResultPath(uint index) => EverythingNative.PtrToStringUni(Native.Everything_GetResultPathW(index));
        public string ResultExtension(uint index) => EverythingNative.PtrToStringUni(Native.Everything_GetResultExtensionW(index));
        public string ResultFileListFileName(uint index) => EverythingNative.PtrToStringUni(Native.Everything_GetResultFileListFileNameW(index));

        public uint ResultFullPathName(uint index, StringBuilder buffer, uint bufferSize) =>
            Native.Everything_GetResultFullPathNameW(index, buffer, bufferSize);

        public uint ResultAttributes(uint index) => Native.Everything_GetResultAttributes(index);
        public uint ResultRunCount(uint index) => Native.Everything_GetResultRunCount(index);

        public bool TryResultSize(uint index, out long size) => Native.Everything_GetResultSize(index, out size);
        public bool TryResultDateCreated(uint index, out long fileTime) => Native.Everything_GetResultDateCreated(index, out fileTime);
        public bool TryResultDateModified(uint index, out long fileTime) => Native.Everything_GetResultDateModified(index, out fileTime);
        public bool TryResultDateAccessed(uint index, out long fileTime) => Native.Everything_GetResultDateAccessed(index, out fileTime);
        public bool TryResultDateRun(uint index, out long fileTime) => Native.Everything_GetResultDateRun(index, out fileTime);
        public bool TryResultDateRecentlyChanged(uint index, out long fileTime) => Native.Everything_GetResultDateRecentlyChanged(index, out fileTime);

        public uint MajorVersion => Native.Everything_GetMajorVersion();
        public uint MinorVersion => Native.Everything_GetMinorVersion();
        public uint Revision => Native.Everything_GetRevision();
        public uint BuildNumber => Native.Everything_GetBuildNumber();
        public uint TargetMachine => Native.Everything_GetTargetMachine();

        public bool IsDBLoaded() => Native.Everything_IsDBLoaded();

        public bool IsBackendAvailable(out string reason)
        {
            try
            {
                // 触发一次动态加载；加载失败会抛 DllNotFoundException。
                _ = Native;
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static NativeExports Native => nativeExports.Value;

        internal static string GetDllFileNameForCurrentProcess()
        {
            return EverythingNative.GetDllFileNameForArchitecture(RuntimeInformation.ProcessArchitecture);
        }

        private static NativeExports LoadNativeExports()
        {
            string dllFileName = GetDllFileNameForCurrentProcess();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            string assemblyDirectory = Path.GetDirectoryName(typeof(EverythingNativeWindows).Assembly.Location) ?? string.Empty;

            if (TryLoadLibrary(Path.Combine(baseDirectory, dllFileName), out IntPtr moduleHandle))
            {
                return new NativeExports(moduleHandle);
            }

            if (!string.Equals(baseDirectory, assemblyDirectory, StringComparison.OrdinalIgnoreCase) &&
                TryLoadLibrary(Path.Combine(assemblyDirectory, dllFileName), out moduleHandle))
            {
                return new NativeExports(moduleHandle);
            }

            if (TryLoadLibrary(dllFileName, out moduleHandle))
            {
                return new NativeExports(moduleHandle);
            }

            throw new DllNotFoundException(
                "Failed to load Everything SDK native library '" + dllFileName + "' for process architecture '" +
                RuntimeInformation.ProcessArchitecture + "'. Searched '" + baseDirectory + "' and '" + assemblyDirectory + "'.");
        }

        private static bool TryLoadLibrary(string path, out IntPtr moduleHandle)
        {
            moduleHandle = NativeLoader.LoadLibrary(path);
            return moduleHandle != IntPtr.Zero;
        }

        private static T GetExport<T>(IntPtr moduleHandle, string exportName)
            where T : class
        {
            IntPtr procAddress = NativeLoader.GetProcAddress(moduleHandle, exportName);
            if (procAddress == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException("Unable to find export '" + exportName + "' in '" + GetDllFileNameForCurrentProcess() + "'.");
            }

            return Marshal.GetDelegateForFunctionPointer(procAddress, typeof(T)) as T
                ?? throw new InvalidOperationException("Unable to bind export '" + exportName + "' to delegate '" + typeof(T).FullName + "'.");
        }

        private sealed class NativeExports
        {
            public readonly Everything_SetSearchWDelegate Everything_SetSearchW;
            public readonly Everything_SetMatchPathDelegate Everything_SetMatchPath;
            public readonly Everything_SetMatchCaseDelegate Everything_SetMatchCase;
            public readonly Everything_SetMatchWholeWordDelegate Everything_SetMatchWholeWord;
            public readonly Everything_SetRegexDelegate Everything_SetRegex;
            public readonly Everything_SetMaxDelegate Everything_SetMax;
            public readonly Everything_SetOffsetDelegate Everything_SetOffset;
            public readonly Everything_SetSortDelegate Everything_SetSort;
            public readonly Everything_SetRequestFlagsDelegate Everything_SetRequestFlags;
            public readonly Everything_QueryWDelegate Everything_QueryW;
            public readonly Everything_GetLastErrorDelegate Everything_GetLastError;
            public readonly Everything_GetTotResultsDelegate Everything_GetTotResults;
            public readonly Everything_GetNumResultsDelegate Everything_GetNumResults;
            public readonly Everything_IsVolumeResultDelegate Everything_IsVolumeResult;
            public readonly Everything_IsFolderResultDelegate Everything_IsFolderResult;
            public readonly Everything_IsFileResultDelegate Everything_IsFileResult;
            public readonly Everything_GetResultFileNameWDelegate Everything_GetResultFileNameW;
            public readonly Everything_GetResultPathWDelegate Everything_GetResultPathW;
            public readonly Everything_GetResultFullPathNameWDelegate Everything_GetResultFullPathNameW;
            public readonly Everything_GetResultExtensionWDelegate Everything_GetResultExtensionW;
            public readonly Everything_GetResultSizeDelegate Everything_GetResultSize;
            public readonly Everything_GetResultDateCreatedDelegate Everything_GetResultDateCreated;
            public readonly Everything_GetResultDateModifiedDelegate Everything_GetResultDateModified;
            public readonly Everything_GetResultDateAccessedDelegate Everything_GetResultDateAccessed;
            public readonly Everything_GetResultAttributesDelegate Everything_GetResultAttributes;
            public readonly Everything_GetResultFileListFileNameWDelegate Everything_GetResultFileListFileNameW;
            public readonly Everything_GetResultRunCountDelegate Everything_GetResultRunCount;
            public readonly Everything_GetResultDateRunDelegate Everything_GetResultDateRun;
            public readonly Everything_GetResultDateRecentlyChangedDelegate Everything_GetResultDateRecentlyChanged;
            public readonly Everything_ResetDelegate Everything_Reset;
            public readonly Everything_CleanUpDelegate Everything_CleanUp;
            public readonly Everything_GetMajorVersionDelegate Everything_GetMajorVersion;
            public readonly Everything_GetMinorVersionDelegate Everything_GetMinorVersion;
            public readonly Everything_GetRevisionDelegate Everything_GetRevision;
            public readonly Everything_GetBuildNumberDelegate Everything_GetBuildNumber;
            public readonly Everything_GetTargetMachineDelegate Everything_GetTargetMachine;
            public readonly Everything_IsDBLoadedDelegate Everything_IsDBLoaded;

            public NativeExports(IntPtr moduleHandle)
            {
                Everything_SetSearchW = GetExport<Everything_SetSearchWDelegate>(moduleHandle, nameof(Everything_SetSearchW));
                Everything_SetMatchPath = GetExport<Everything_SetMatchPathDelegate>(moduleHandle, nameof(Everything_SetMatchPath));
                Everything_SetMatchCase = GetExport<Everything_SetMatchCaseDelegate>(moduleHandle, nameof(Everything_SetMatchCase));
                Everything_SetMatchWholeWord = GetExport<Everything_SetMatchWholeWordDelegate>(moduleHandle, nameof(Everything_SetMatchWholeWord));
                Everything_SetRegex = GetExport<Everything_SetRegexDelegate>(moduleHandle, nameof(Everything_SetRegex));
                Everything_SetMax = GetExport<Everything_SetMaxDelegate>(moduleHandle, nameof(Everything_SetMax));
                Everything_SetOffset = GetExport<Everything_SetOffsetDelegate>(moduleHandle, nameof(Everything_SetOffset));
                Everything_SetSort = GetExport<Everything_SetSortDelegate>(moduleHandle, nameof(Everything_SetSort));
                Everything_SetRequestFlags = GetExport<Everything_SetRequestFlagsDelegate>(moduleHandle, nameof(Everything_SetRequestFlags));
                Everything_QueryW = GetExport<Everything_QueryWDelegate>(moduleHandle, nameof(Everything_QueryW));
                Everything_GetLastError = GetExport<Everything_GetLastErrorDelegate>(moduleHandle, nameof(Everything_GetLastError));
                Everything_GetTotResults = GetExport<Everything_GetTotResultsDelegate>(moduleHandle, nameof(Everything_GetTotResults));
                Everything_GetNumResults = GetExport<Everything_GetNumResultsDelegate>(moduleHandle, nameof(Everything_GetNumResults));
                Everything_IsVolumeResult = GetExport<Everything_IsVolumeResultDelegate>(moduleHandle, nameof(Everything_IsVolumeResult));
                Everything_IsFolderResult = GetExport<Everything_IsFolderResultDelegate>(moduleHandle, nameof(Everything_IsFolderResult));
                Everything_IsFileResult = GetExport<Everything_IsFileResultDelegate>(moduleHandle, nameof(Everything_IsFileResult));
                Everything_GetResultFileNameW = GetExport<Everything_GetResultFileNameWDelegate>(moduleHandle, nameof(Everything_GetResultFileNameW));
                Everything_GetResultPathW = GetExport<Everything_GetResultPathWDelegate>(moduleHandle, nameof(Everything_GetResultPathW));
                Everything_GetResultFullPathNameW = GetExport<Everything_GetResultFullPathNameWDelegate>(moduleHandle, nameof(Everything_GetResultFullPathNameW));
                Everything_GetResultExtensionW = GetExport<Everything_GetResultExtensionWDelegate>(moduleHandle, nameof(Everything_GetResultExtensionW));
                Everything_GetResultSize = GetExport<Everything_GetResultSizeDelegate>(moduleHandle, nameof(Everything_GetResultSize));
                Everything_GetResultDateCreated = GetExport<Everything_GetResultDateCreatedDelegate>(moduleHandle, nameof(Everything_GetResultDateCreated));
                Everything_GetResultDateModified = GetExport<Everything_GetResultDateModifiedDelegate>(moduleHandle, nameof(Everything_GetResultDateModified));
                Everything_GetResultDateAccessed = GetExport<Everything_GetResultDateAccessedDelegate>(moduleHandle, nameof(Everything_GetResultDateAccessed));
                Everything_GetResultAttributes = GetExport<Everything_GetResultAttributesDelegate>(moduleHandle, nameof(Everything_GetResultAttributes));
                Everything_GetResultFileListFileNameW = GetExport<Everything_GetResultFileListFileNameWDelegate>(moduleHandle, nameof(Everything_GetResultFileListFileNameW));
                Everything_GetResultRunCount = GetExport<Everything_GetResultRunCountDelegate>(moduleHandle, nameof(Everything_GetResultRunCount));
                Everything_GetResultDateRun = GetExport<Everything_GetResultDateRunDelegate>(moduleHandle, nameof(Everything_GetResultDateRun));
                Everything_GetResultDateRecentlyChanged = GetExport<Everything_GetResultDateRecentlyChangedDelegate>(moduleHandle, nameof(Everything_GetResultDateRecentlyChanged));
                Everything_Reset = GetExport<Everything_ResetDelegate>(moduleHandle, nameof(Everything_Reset));
                Everything_CleanUp = GetExport<Everything_CleanUpDelegate>(moduleHandle, nameof(Everything_CleanUp));
                Everything_GetMajorVersion = GetExport<Everything_GetMajorVersionDelegate>(moduleHandle, nameof(Everything_GetMajorVersion));
                Everything_GetMinorVersion = GetExport<Everything_GetMinorVersionDelegate>(moduleHandle, nameof(Everything_GetMinorVersion));
                Everything_GetRevision = GetExport<Everything_GetRevisionDelegate>(moduleHandle, nameof(Everything_GetRevision));
                Everything_GetBuildNumber = GetExport<Everything_GetBuildNumberDelegate>(moduleHandle, nameof(Everything_GetBuildNumber));
                Everything_GetTargetMachine = GetExport<Everything_GetTargetMachineDelegate>(moduleHandle, nameof(Everything_GetTargetMachine));
                Everything_IsDBLoaded = GetExport<Everything_IsDBLoadedDelegate>(moduleHandle, nameof(Everything_IsDBLoaded));
            }
        }

        private static class NativeLoader
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryW")]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate uint Everything_SetSearchWDelegate(string lpSearchString);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetMatchPathDelegate(bool bEnable);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetMatchCaseDelegate(bool bEnable);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetMatchWholeWordDelegate(bool bEnable);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetRegexDelegate(bool bEnable);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetMaxDelegate(uint dwMax);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetOffsetDelegate(uint dwOffset);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetSortDelegate(uint dwSortType);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetRequestFlagsDelegate(uint dwRequestFlags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool Everything_QueryWDelegate(bool bWait);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetLastErrorDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetTotResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetNumResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsVolumeResultDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsFolderResultDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsFileResultDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultFileNameWDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultPathWDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate uint Everything_GetResultFullPathNameWDelegate(uint nIndex, StringBuilder buf, uint bufsize);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultExtensionWDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetResultSizeDelegate(uint nIndex, out long lpFileSize);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetResultDateCreatedDelegate(uint nIndex, out long lpFileTime);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetResultDateModifiedDelegate(uint nIndex, out long lpFileTime);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetResultDateAccessedDelegate(uint nIndex, out long lpFileTime);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetResultAttributesDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultFileListFileNameWDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetResultRunCountDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetResultDateRunDelegate(uint nIndex, out long lpFileTime);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetResultDateRecentlyChangedDelegate(uint nIndex, out long lpFileTime);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_ResetDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_CleanUpDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetMajorVersionDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetMinorVersionDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetRevisionDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetBuildNumberDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetTargetMachineDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsDBLoadedDelegate();
    }
}
