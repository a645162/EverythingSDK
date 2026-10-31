// File: EverythingEnums.cs
// Brief: EverythingEnums.
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-07-31

using System;

namespace Lib.Everything
{
    /// <summary>SDK 错误码，对应 Everything_GetLastError。</summary>
    public enum EverythingError : uint
    {
        Ok = 0,
        Memory = 1,
        Ipc = 2,
        RegisterClassEx = 3,
        CreateWindow = 4,
        CreateThread = 5,
        InvalidIndex = 6,
        InvalidCall = 7,
        InvalidRequest = 8,
        InvalidParameter = 9
    }

    /// <summary>结果排序方式，对应 Everything_SetSort。</summary>
    public enum EverythingSort : uint
    {
        NameAscending = 1,
        NameDescending = 2,
        PathAscending = 3,
        PathDescending = 4,
        SizeAscending = 5,
        SizeDescending = 6,
        ExtensionAscending = 7,
        ExtensionDescending = 8,
        TypeNameAscending = 9,
        TypeNameDescending = 10,
        DateCreatedAscending = 11,
        DateCreatedDescending = 12,
        DateModifiedAscending = 13,
        DateModifiedDescending = 14,
        AttributesAscending = 15,
        AttributesDescending = 16,
        FileListFileNameAscending = 17,
        FileListFileNameDescending = 18,
        RunCountAscending = 19,
        RunCountDescending = 20,
        DateRecentlyChangedAscending = 21,
        DateRecentlyChangedDescending = 22,
        DateAccessedAscending = 23,
        DateAccessedDescending = 24,
        DateRunAscending = 25,
        DateRunDescending = 26
    }

    /// <summary>结果请求标志，对应 Everything_SetRequestFlags，可按位或。</summary>
    [Flags]
    public enum EverythingRequestFlags : uint
    {
        FileName = 0x00000001,
        Path = 0x00000002,
        FullPathAndFileName = 0x00000004,
        Extension = 0x00000008,
        Size = 0x00000010,
        DateCreated = 0x00000020,
        DateModified = 0x00000040,
        DateAccessed = 0x00000080,
        Attributes = 0x00000100,
        FileListFileName = 0x00000200,
        RunCount = 0x00000400,
        DateRun = 0x00000800,
        DateRecentlyChanged = 0x00001000,
        HighlightedFileName = 0x00002000,
        HighlightedPath = 0x00004000,
        HighlightedFullPathAndFileName = 0x00008000
    }

    /// <summary>目标机器类型，对应 Everything_GetTargetMachine。</summary>
    public enum EverythingTargetMachine : uint
    {
        X86 = 1,
        X64 = 2,
        Arm = 3,
        Arm64 = 4
    }
}
