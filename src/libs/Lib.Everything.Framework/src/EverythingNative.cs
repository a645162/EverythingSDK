// File: libs/EverythingSDK/src/libs/Lib.Everything.Framework/src/EverythingNative.cs
// Brief: 托管后端入口：按运行平台选择 Windows 原生库或 POSIX locate/find 后端。
// Author: Haomin Kong
// Email: konghm@beim.com.cn / a645162@163.com
// Created: 2026-10-31

using System;
using System.Runtime.InteropServices;

namespace Lib.Everything
{
    /// <summary>
    /// Everything 后端的统一入口。
    /// Windows 使用 Everything*.dll；POSIX 使用系统 locate 数据库、find 或进程内扫描。
    /// </summary>
    internal static class EverythingNative
    {
        private static readonly Lazy<IEverythingBackend> lazyBackend =
            new Lazy<IEverythingBackend>(CreateBackend, true);

        internal static IEverythingBackend Backend => lazyBackend.Value;

        private static IEverythingBackend CreateBackend()
        {
#if !NETFRAMEWORK
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new EverythingNativePosix();
            }
#endif
            return new EverythingNativeWindows();
        }

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
    }
}
