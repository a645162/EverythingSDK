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
    internal static class EverythingNative
    {
        private static readonly Lazy<NativeExports> nativeExports = new Lazy<NativeExports>(LoadNativeExports, true);

        public static uint Everything_SetSearchW(string lpSearchString) => Native.Everything_SetSearchW(lpSearchString);
        public static void Everything_SetMatchPath(bool bEnable) => Native.Everything_SetMatchPath(bEnable);
        public static void Everything_SetMatchCase(bool bEnable) => Native.Everything_SetMatchCase(bEnable);
        public static void Everything_SetMatchWholeWord(bool bEnable) => Native.Everything_SetMatchWholeWord(bEnable);
        public static void Everything_SetRegex(bool bEnable) => Native.Everything_SetRegex(bEnable);
        public static void Everything_SetMax(uint dwMax) => Native.Everything_SetMax(dwMax);
        public static void Everything_SetOffset(uint dwOffset) => Native.Everything_SetOffset(dwOffset);
        public static void Everything_SetReplyWindow(IntPtr hWnd) => Native.Everything_SetReplyWindow(hWnd);
        public static void Everything_SetReplyID(uint dwId) => Native.Everything_SetReplyID(dwId);
        public static void Everything_SetSort(uint dwSortType) => Native.Everything_SetSort(dwSortType);
        public static void Everything_SetRequestFlags(uint dwRequestFlags) => Native.Everything_SetRequestFlags(dwRequestFlags);

        public static bool Everything_GetMatchPath() => Native.Everything_GetMatchPath();
        public static bool Everything_GetMatchCase() => Native.Everything_GetMatchCase();
        public static bool Everything_GetMatchWholeWord() => Native.Everything_GetMatchWholeWord();
        public static bool Everything_GetRegex() => Native.Everything_GetRegex();
        public static uint Everything_GetMax() => Native.Everything_GetMax();
        public static uint Everything_GetOffset() => Native.Everything_GetOffset();
        public static IntPtr Everything_GetSearchW() => Native.Everything_GetSearchW();
        public static uint Everything_GetLastError() => Native.Everything_GetLastError();
        public static IntPtr Everything_GetReplyWindow() => Native.Everything_GetReplyWindow();
        public static uint Everything_GetReplyID() => Native.Everything_GetReplyID();
        public static uint Everything_GetSort() => Native.Everything_GetSort();
        public static uint Everything_GetRequestFlags() => Native.Everything_GetRequestFlags();

        public static bool Everything_QueryW(bool bWait) => Native.Everything_QueryW(bWait);
        public static bool Everything_IsQueryReply(uint message, IntPtr wParam, IntPtr lParam, uint dwId) => Native.Everything_IsQueryReply(message, wParam, lParam, dwId);

        public static void Everything_SortResultsByPath() => Native.Everything_SortResultsByPath();

        public static uint Everything_GetNumFileResults() => Native.Everything_GetNumFileResults();
        public static uint Everything_GetNumFolderResults() => Native.Everything_GetNumFolderResults();
        public static uint Everything_GetNumResults() => Native.Everything_GetNumResults();
        public static uint Everything_GetTotFileResults() => Native.Everything_GetTotFileResults();
        public static uint Everything_GetTotFolderResults() => Native.Everything_GetTotFolderResults();
        public static uint Everything_GetTotResults() => Native.Everything_GetTotResults();
        public static bool Everything_IsVolumeResult(uint nIndex) => Native.Everything_IsVolumeResult(nIndex);
        public static bool Everything_IsFolderResult(uint nIndex) => Native.Everything_IsFolderResult(nIndex);
        public static bool Everything_IsFileResult(uint nIndex) => Native.Everything_IsFileResult(nIndex);
        public static IntPtr Everything_GetResultFileNameW(uint nIndex) => Native.Everything_GetResultFileNameW(nIndex);
        public static IntPtr Everything_GetResultPathW(uint nIndex) => Native.Everything_GetResultPathW(nIndex);
        public static uint Everything_GetResultFullPathNameW(uint nIndex, StringBuilder buf, uint bufsize) => Native.Everything_GetResultFullPathNameW(nIndex, buf, bufsize);
        public static uint Everything_GetResultListSort() => Native.Everything_GetResultListSort();
        public static uint Everything_GetResultListRequestFlags() => Native.Everything_GetResultListRequestFlags();
        public static IntPtr Everything_GetResultExtensionW(uint nIndex) => Native.Everything_GetResultExtensionW(nIndex);
        public static bool Everything_GetResultSize(uint nIndex, out long lpFileSize) => Native.Everything_GetResultSize(nIndex, out lpFileSize);
        public static bool Everything_GetResultDateCreated(uint nIndex, out long lpFileTime) => Native.Everything_GetResultDateCreated(nIndex, out lpFileTime);
        public static bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime) => Native.Everything_GetResultDateModified(nIndex, out lpFileTime);
        public static bool Everything_GetResultDateAccessed(uint nIndex, out long lpFileTime) => Native.Everything_GetResultDateAccessed(nIndex, out lpFileTime);
        public static uint Everything_GetResultAttributes(uint nIndex) => Native.Everything_GetResultAttributes(nIndex);
        public static IntPtr Everything_GetResultFileListFileNameW(uint nIndex) => Native.Everything_GetResultFileListFileNameW(nIndex);
        public static uint Everything_GetResultRunCount(uint nIndex) => Native.Everything_GetResultRunCount(nIndex);
        public static bool Everything_GetResultDateRun(uint nIndex, out long lpFileTime) => Native.Everything_GetResultDateRun(nIndex, out lpFileTime);
        public static bool Everything_GetResultDateRecentlyChanged(uint nIndex, out long lpFileTime) => Native.Everything_GetResultDateRecentlyChanged(nIndex, out lpFileTime);
        public static IntPtr Everything_GetResultHighlightedFileNameW(uint nIndex) => Native.Everything_GetResultHighlightedFileNameW(nIndex);
        public static IntPtr Everything_GetResultHighlightedPathW(uint nIndex) => Native.Everything_GetResultHighlightedPathW(nIndex);
        public static IntPtr Everything_GetResultHighlightedFullPathAndFileNameW(uint nIndex) => Native.Everything_GetResultHighlightedFullPathAndFileNameW(nIndex);

        public static void Everything_Reset() => Native.Everything_Reset();
        public static void Everything_CleanUp() => Native.Everything_CleanUp();

        public static uint Everything_GetMajorVersion() => Native.Everything_GetMajorVersion();
        public static uint Everything_GetMinorVersion() => Native.Everything_GetMinorVersion();
        public static uint Everything_GetRevision() => Native.Everything_GetRevision();
        public static uint Everything_GetBuildNumber() => Native.Everything_GetBuildNumber();
        public static bool Everything_Exit() => Native.Everything_Exit();
        public static bool Everything_IsDBLoaded() => Native.Everything_IsDBLoaded();
        public static bool Everything_IsAdmin() => Native.Everything_IsAdmin();
        public static bool Everything_IsAppData() => Native.Everything_IsAppData();
        public static bool Everything_RebuildDB() => Native.Everything_RebuildDB();
        public static bool Everything_UpdateAllFolderIndexes() => Native.Everything_UpdateAllFolderIndexes();
        public static bool Everything_SaveDB() => Native.Everything_SaveDB();
        public static bool Everything_SaveRunHistory() => Native.Everything_SaveRunHistory();
        public static bool Everything_DeleteRunHistory() => Native.Everything_DeleteRunHistory();
        public static uint Everything_GetTargetMachine() => Native.Everything_GetTargetMachine();
        public static bool Everything_IsFastSort(uint sortType) => Native.Everything_IsFastSort(sortType);
        public static bool Everything_IsFileInfoIndexed(uint fileInfoType) => Native.Everything_IsFileInfoIndexed(fileInfoType);

        public static uint Everything_GetRunCountFromFileNameW(string lpFileName) => Native.Everything_GetRunCountFromFileNameW(lpFileName);
        public static bool Everything_SetRunCountFromFileNameW(string lpFileName, uint dwRunCount) => Native.Everything_SetRunCountFromFileNameW(lpFileName, dwRunCount);
        public static uint Everything_IncRunCountFromFileNameW(string lpFileName) => Native.Everything_IncRunCountFromFileNameW(lpFileName);

        private static NativeExports Native => nativeExports.Value;

        internal static string GetDllFileNameForCurrentProcess()
        {
            return GetDllFileNameForArchitecture(RuntimeInformation.ProcessArchitecture);
        }

        internal static string GetDllFileNameForArchitecture(Architecture architecture)
        {
            switch (architecture)
            {
                case Architecture.X86:
                    return "Everything32.dll";
                case Architecture.X64:
                    return "Everything64.dll";
                case Architecture.Arm:
                    return "EverythingARM.dll";
                case Architecture.Arm64:
                    return "EverythingARM64.dll";
                default:
                    throw new NotSupportedException("Unsupported process architecture: " + architecture + ".");
            }
        }

        /// <summary>把原生指针按 Unicode 字符串读出，空指针返回 null。</summary>
        public static string PtrToStringUni(IntPtr p)
        {
            return p == IntPtr.Zero ? null : Marshal.PtrToStringUni(p);
        }

        private static NativeExports LoadNativeExports()
        {
            string dllFileName = GetDllFileNameForCurrentProcess();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            string assemblyDirectory = Path.GetDirectoryName(typeof(EverythingNative).Assembly.Location) ?? string.Empty;

            IntPtr moduleHandle;
            string loadedPath;

            if (TryLoadLibrary(Path.Combine(baseDirectory, dllFileName), out moduleHandle))
            {
                loadedPath = Path.Combine(baseDirectory, dllFileName);
                return new NativeExports(moduleHandle, loadedPath);
            }

            if (!string.Equals(baseDirectory, assemblyDirectory, StringComparison.OrdinalIgnoreCase) &&
                TryLoadLibrary(Path.Combine(assemblyDirectory, dllFileName), out moduleHandle))
            {
                loadedPath = Path.Combine(assemblyDirectory, dllFileName);
                return new NativeExports(moduleHandle, loadedPath);
            }

            if (TryLoadLibrary(dllFileName, out moduleHandle))
            {
                return new NativeExports(moduleHandle, dllFileName);
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
            public readonly Everything_SetReplyWindowDelegate Everything_SetReplyWindow;
            public readonly Everything_SetReplyIDDelegate Everything_SetReplyID;
            public readonly Everything_SetSortDelegate Everything_SetSort;
            public readonly Everything_SetRequestFlagsDelegate Everything_SetRequestFlags;
            public readonly Everything_GetMatchPathDelegate Everything_GetMatchPath;
            public readonly Everything_GetMatchCaseDelegate Everything_GetMatchCase;
            public readonly Everything_GetMatchWholeWordDelegate Everything_GetMatchWholeWord;
            public readonly Everything_GetRegexDelegate Everything_GetRegex;
            public readonly Everything_GetMaxDelegate Everything_GetMax;
            public readonly Everything_GetOffsetDelegate Everything_GetOffset;
            public readonly Everything_GetSearchWDelegate Everything_GetSearchW;
            public readonly Everything_GetLastErrorDelegate Everything_GetLastError;
            public readonly Everything_GetReplyWindowDelegate Everything_GetReplyWindow;
            public readonly Everything_GetReplyIDDelegate Everything_GetReplyID;
            public readonly Everything_GetSortDelegate Everything_GetSort;
            public readonly Everything_GetRequestFlagsDelegate Everything_GetRequestFlags;
            public readonly Everything_QueryWDelegate Everything_QueryW;
            public readonly Everything_IsQueryReplyDelegate Everything_IsQueryReply;
            public readonly Everything_SortResultsByPathDelegate Everything_SortResultsByPath;
            public readonly Everything_GetNumFileResultsDelegate Everything_GetNumFileResults;
            public readonly Everything_GetNumFolderResultsDelegate Everything_GetNumFolderResults;
            public readonly Everything_GetNumResultsDelegate Everything_GetNumResults;
            public readonly Everything_GetTotFileResultsDelegate Everything_GetTotFileResults;
            public readonly Everything_GetTotFolderResultsDelegate Everything_GetTotFolderResults;
            public readonly Everything_GetTotResultsDelegate Everything_GetTotResults;
            public readonly Everything_IsVolumeResultDelegate Everything_IsVolumeResult;
            public readonly Everything_IsFolderResultDelegate Everything_IsFolderResult;
            public readonly Everything_IsFileResultDelegate Everything_IsFileResult;
            public readonly Everything_GetResultFileNameWDelegate Everything_GetResultFileNameW;
            public readonly Everything_GetResultPathWDelegate Everything_GetResultPathW;
            public readonly Everything_GetResultFullPathNameWDelegate Everything_GetResultFullPathNameW;
            public readonly Everything_GetResultListSortDelegate Everything_GetResultListSort;
            public readonly Everything_GetResultListRequestFlagsDelegate Everything_GetResultListRequestFlags;
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
            public readonly Everything_GetResultHighlightedFileNameWDelegate Everything_GetResultHighlightedFileNameW;
            public readonly Everything_GetResultHighlightedPathWDelegate Everything_GetResultHighlightedPathW;
            public readonly Everything_GetResultHighlightedFullPathAndFileNameWDelegate Everything_GetResultHighlightedFullPathAndFileNameW;
            public readonly Everything_ResetDelegate Everything_Reset;
            public readonly Everything_CleanUpDelegate Everything_CleanUp;
            public readonly Everything_GetMajorVersionDelegate Everything_GetMajorVersion;
            public readonly Everything_GetMinorVersionDelegate Everything_GetMinorVersion;
            public readonly Everything_GetRevisionDelegate Everything_GetRevision;
            public readonly Everything_GetBuildNumberDelegate Everything_GetBuildNumber;
            public readonly Everything_ExitDelegate Everything_Exit;
            public readonly Everything_IsDBLoadedDelegate Everything_IsDBLoaded;
            public readonly Everything_IsAdminDelegate Everything_IsAdmin;
            public readonly Everything_IsAppDataDelegate Everything_IsAppData;
            public readonly Everything_RebuildDBDelegate Everything_RebuildDB;
            public readonly Everything_UpdateAllFolderIndexesDelegate Everything_UpdateAllFolderIndexes;
            public readonly Everything_SaveDBDelegate Everything_SaveDB;
            public readonly Everything_SaveRunHistoryDelegate Everything_SaveRunHistory;
            public readonly Everything_DeleteRunHistoryDelegate Everything_DeleteRunHistory;
            public readonly Everything_GetTargetMachineDelegate Everything_GetTargetMachine;
            public readonly Everything_IsFastSortDelegate Everything_IsFastSort;
            public readonly Everything_IsFileInfoIndexedDelegate Everything_IsFileInfoIndexed;
            public readonly Everything_GetRunCountFromFileNameWDelegate Everything_GetRunCountFromFileNameW;
            public readonly Everything_SetRunCountFromFileNameWDelegate Everything_SetRunCountFromFileNameW;
            public readonly Everything_IncRunCountFromFileNameWDelegate Everything_IncRunCountFromFileNameW;

            public NativeExports(IntPtr moduleHandle, string loadedPath)
            {
                _ = loadedPath;

                Everything_SetSearchW = GetExport<Everything_SetSearchWDelegate>(moduleHandle, nameof(Everything_SetSearchW));
                Everything_SetMatchPath = GetExport<Everything_SetMatchPathDelegate>(moduleHandle, nameof(Everything_SetMatchPath));
                Everything_SetMatchCase = GetExport<Everything_SetMatchCaseDelegate>(moduleHandle, nameof(Everything_SetMatchCase));
                Everything_SetMatchWholeWord = GetExport<Everything_SetMatchWholeWordDelegate>(moduleHandle, nameof(Everything_SetMatchWholeWord));
                Everything_SetRegex = GetExport<Everything_SetRegexDelegate>(moduleHandle, nameof(Everything_SetRegex));
                Everything_SetMax = GetExport<Everything_SetMaxDelegate>(moduleHandle, nameof(Everything_SetMax));
                Everything_SetOffset = GetExport<Everything_SetOffsetDelegate>(moduleHandle, nameof(Everything_SetOffset));
                Everything_SetReplyWindow = GetExport<Everything_SetReplyWindowDelegate>(moduleHandle, nameof(Everything_SetReplyWindow));
                Everything_SetReplyID = GetExport<Everything_SetReplyIDDelegate>(moduleHandle, nameof(Everything_SetReplyID));
                Everything_SetSort = GetExport<Everything_SetSortDelegate>(moduleHandle, nameof(Everything_SetSort));
                Everything_SetRequestFlags = GetExport<Everything_SetRequestFlagsDelegate>(moduleHandle, nameof(Everything_SetRequestFlags));
                Everything_GetMatchPath = GetExport<Everything_GetMatchPathDelegate>(moduleHandle, nameof(Everything_GetMatchPath));
                Everything_GetMatchCase = GetExport<Everything_GetMatchCaseDelegate>(moduleHandle, nameof(Everything_GetMatchCase));
                Everything_GetMatchWholeWord = GetExport<Everything_GetMatchWholeWordDelegate>(moduleHandle, nameof(Everything_GetMatchWholeWord));
                Everything_GetRegex = GetExport<Everything_GetRegexDelegate>(moduleHandle, nameof(Everything_GetRegex));
                Everything_GetMax = GetExport<Everything_GetMaxDelegate>(moduleHandle, nameof(Everything_GetMax));
                Everything_GetOffset = GetExport<Everything_GetOffsetDelegate>(moduleHandle, nameof(Everything_GetOffset));
                Everything_GetSearchW = GetExport<Everything_GetSearchWDelegate>(moduleHandle, nameof(Everything_GetSearchW));
                Everything_GetLastError = GetExport<Everything_GetLastErrorDelegate>(moduleHandle, nameof(Everything_GetLastError));
                Everything_GetReplyWindow = GetExport<Everything_GetReplyWindowDelegate>(moduleHandle, nameof(Everything_GetReplyWindow));
                Everything_GetReplyID = GetExport<Everything_GetReplyIDDelegate>(moduleHandle, nameof(Everything_GetReplyID));
                Everything_GetSort = GetExport<Everything_GetSortDelegate>(moduleHandle, nameof(Everything_GetSort));
                Everything_GetRequestFlags = GetExport<Everything_GetRequestFlagsDelegate>(moduleHandle, nameof(Everything_GetRequestFlags));
                Everything_QueryW = GetExport<Everything_QueryWDelegate>(moduleHandle, nameof(Everything_QueryW));
                Everything_IsQueryReply = GetExport<Everything_IsQueryReplyDelegate>(moduleHandle, nameof(Everything_IsQueryReply));
                Everything_SortResultsByPath = GetExport<Everything_SortResultsByPathDelegate>(moduleHandle, nameof(Everything_SortResultsByPath));
                Everything_GetNumFileResults = GetExport<Everything_GetNumFileResultsDelegate>(moduleHandle, nameof(Everything_GetNumFileResults));
                Everything_GetNumFolderResults = GetExport<Everything_GetNumFolderResultsDelegate>(moduleHandle, nameof(Everything_GetNumFolderResults));
                Everything_GetNumResults = GetExport<Everything_GetNumResultsDelegate>(moduleHandle, nameof(Everything_GetNumResults));
                Everything_GetTotFileResults = GetExport<Everything_GetTotFileResultsDelegate>(moduleHandle, nameof(Everything_GetTotFileResults));
                Everything_GetTotFolderResults = GetExport<Everything_GetTotFolderResultsDelegate>(moduleHandle, nameof(Everything_GetTotFolderResults));
                Everything_GetTotResults = GetExport<Everything_GetTotResultsDelegate>(moduleHandle, nameof(Everything_GetTotResults));
                Everything_IsVolumeResult = GetExport<Everything_IsVolumeResultDelegate>(moduleHandle, nameof(Everything_IsVolumeResult));
                Everything_IsFolderResult = GetExport<Everything_IsFolderResultDelegate>(moduleHandle, nameof(Everything_IsFolderResult));
                Everything_IsFileResult = GetExport<Everything_IsFileResultDelegate>(moduleHandle, nameof(Everything_IsFileResult));
                Everything_GetResultFileNameW = GetExport<Everything_GetResultFileNameWDelegate>(moduleHandle, nameof(Everything_GetResultFileNameW));
                Everything_GetResultPathW = GetExport<Everything_GetResultPathWDelegate>(moduleHandle, nameof(Everything_GetResultPathW));
                Everything_GetResultFullPathNameW = GetExport<Everything_GetResultFullPathNameWDelegate>(moduleHandle, nameof(Everything_GetResultFullPathNameW));
                Everything_GetResultListSort = GetExport<Everything_GetResultListSortDelegate>(moduleHandle, nameof(Everything_GetResultListSort));
                Everything_GetResultListRequestFlags = GetExport<Everything_GetResultListRequestFlagsDelegate>(moduleHandle, nameof(Everything_GetResultListRequestFlags));
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
                Everything_GetResultHighlightedFileNameW = GetExport<Everything_GetResultHighlightedFileNameWDelegate>(moduleHandle, nameof(Everything_GetResultHighlightedFileNameW));
                Everything_GetResultHighlightedPathW = GetExport<Everything_GetResultHighlightedPathWDelegate>(moduleHandle, nameof(Everything_GetResultHighlightedPathW));
                Everything_GetResultHighlightedFullPathAndFileNameW = GetExport<Everything_GetResultHighlightedFullPathAndFileNameWDelegate>(moduleHandle, nameof(Everything_GetResultHighlightedFullPathAndFileNameW));
                Everything_Reset = GetExport<Everything_ResetDelegate>(moduleHandle, nameof(Everything_Reset));
                Everything_CleanUp = GetExport<Everything_CleanUpDelegate>(moduleHandle, nameof(Everything_CleanUp));
                Everything_GetMajorVersion = GetExport<Everything_GetMajorVersionDelegate>(moduleHandle, nameof(Everything_GetMajorVersion));
                Everything_GetMinorVersion = GetExport<Everything_GetMinorVersionDelegate>(moduleHandle, nameof(Everything_GetMinorVersion));
                Everything_GetRevision = GetExport<Everything_GetRevisionDelegate>(moduleHandle, nameof(Everything_GetRevision));
                Everything_GetBuildNumber = GetExport<Everything_GetBuildNumberDelegate>(moduleHandle, nameof(Everything_GetBuildNumber));
                Everything_Exit = GetExport<Everything_ExitDelegate>(moduleHandle, nameof(Everything_Exit));
                Everything_IsDBLoaded = GetExport<Everything_IsDBLoadedDelegate>(moduleHandle, nameof(Everything_IsDBLoaded));
                Everything_IsAdmin = GetExport<Everything_IsAdminDelegate>(moduleHandle, nameof(Everything_IsAdmin));
                Everything_IsAppData = GetExport<Everything_IsAppDataDelegate>(moduleHandle, nameof(Everything_IsAppData));
                Everything_RebuildDB = GetExport<Everything_RebuildDBDelegate>(moduleHandle, nameof(Everything_RebuildDB));
                Everything_UpdateAllFolderIndexes = GetExport<Everything_UpdateAllFolderIndexesDelegate>(moduleHandle, nameof(Everything_UpdateAllFolderIndexes));
                Everything_SaveDB = GetExport<Everything_SaveDBDelegate>(moduleHandle, nameof(Everything_SaveDB));
                Everything_SaveRunHistory = GetExport<Everything_SaveRunHistoryDelegate>(moduleHandle, nameof(Everything_SaveRunHistory));
                Everything_DeleteRunHistory = GetExport<Everything_DeleteRunHistoryDelegate>(moduleHandle, nameof(Everything_DeleteRunHistory));
                Everything_GetTargetMachine = GetExport<Everything_GetTargetMachineDelegate>(moduleHandle, nameof(Everything_GetTargetMachine));
                Everything_IsFastSort = GetExport<Everything_IsFastSortDelegate>(moduleHandle, nameof(Everything_IsFastSort));
                Everything_IsFileInfoIndexed = GetExport<Everything_IsFileInfoIndexedDelegate>(moduleHandle, nameof(Everything_IsFileInfoIndexed));
                Everything_GetRunCountFromFileNameW = GetExport<Everything_GetRunCountFromFileNameWDelegate>(moduleHandle, nameof(Everything_GetRunCountFromFileNameW));
                Everything_SetRunCountFromFileNameW = GetExport<Everything_SetRunCountFromFileNameWDelegate>(moduleHandle, nameof(Everything_SetRunCountFromFileNameW));
                Everything_IncRunCountFromFileNameW = GetExport<Everything_IncRunCountFromFileNameWDelegate>(moduleHandle, nameof(Everything_IncRunCountFromFileNameW));
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
        private delegate void Everything_SetReplyWindowDelegate(IntPtr hWnd);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetReplyIDDelegate(uint dwId);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetSortDelegate(uint dwSortType);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SetRequestFlagsDelegate(uint dwRequestFlags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetMatchPathDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetMatchCaseDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetMatchWholeWordDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_GetRegexDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetMaxDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetOffsetDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr Everything_GetSearchWDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetLastErrorDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr Everything_GetReplyWindowDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetReplyIDDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetSortDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetRequestFlagsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool Everything_QueryWDelegate(bool bWait);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsQueryReplyDelegate(uint message, IntPtr wParam, IntPtr lParam, uint dwId);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void Everything_SortResultsByPathDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetNumFileResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetNumFolderResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetNumResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetTotFileResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetTotFolderResultsDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetTotResultsDelegate();
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
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetResultListSortDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetResultListRequestFlagsDelegate();
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
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultHighlightedFileNameWDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultHighlightedPathWDelegate(uint nIndex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate IntPtr Everything_GetResultHighlightedFullPathAndFileNameWDelegate(uint nIndex);
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
        private delegate bool Everything_ExitDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsDBLoadedDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsAdminDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsAppDataDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_RebuildDBDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_UpdateAllFolderIndexesDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_SaveDBDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_SaveRunHistoryDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_DeleteRunHistoryDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint Everything_GetTargetMachineDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsFastSortDelegate(uint sortType);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool Everything_IsFileInfoIndexedDelegate(uint fileInfoType);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate uint Everything_GetRunCountFromFileNameWDelegate(string lpFileName);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool Everything_SetRunCountFromFileNameWDelegate(string lpFileName, uint dwRunCount);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate uint Everything_IncRunCountFromFileNameWDelegate(string lpFileName);
    }
}
