// File: EverythingNativePosixEntry.cs
// Brief: POSIX 后端条目模型：元数据读取、排序与比较。
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-10-31
//
// 说明：该文件仅在非 .NET Framework 目标中参与编译
// （netstandard2.0 / net6.0 / net8.0 / net10.0）；
// .NET Framework 目标是 Windows 专用，不需要也不应该编译本后端。

#if !NETFRAMEWORK

using System;
using System.IO;

namespace Lib.Everything
{
    internal sealed partial class EverythingNativePosix
    {
        // ------------------------------------------------------------------
        // 元数据
        // ------------------------------------------------------------------
        private sealed class Entry
        {
            public Entry(string path)
            {
                Path = path;
                FileName = System.IO.Path.GetFileName(path);
                Directory = System.IO.Path.GetDirectoryName(path) ?? "/";
                string extension = System.IO.Path.GetExtension(path);
                Extension = extension.Length > 0 ? extension.Substring(1) : string.Empty;
            }

            public string Path { get; }
            public string FileName { get; }
            public string Directory { get; }
            public string Extension { get; }

            public bool MetadataLoaded;
            public bool Exists;
            public bool IsFile;
            public bool IsFolder;
            public long Size;
            public uint Attributes;
            public DateTime? Created;
            public DateTime? Modified;
            public DateTime? Accessed;
        }

        private static void EnsureMetadata(Entry entry)
        {
            if (entry.MetadataLoaded)
            {
                return;
            }
            entry.MetadataLoaded = true;

            try
            {
                FileAttributes attributes = File.GetAttributes(entry.Path);
                entry.Exists = true;
                entry.Attributes = (uint)attributes;
                entry.IsFolder = (attributes & FileAttributes.Directory) != 0;
                entry.IsFile = !entry.IsFolder;

                var info = new FileInfo(entry.Path);
                if (info.Exists)
                {
                    entry.Size = info.Length;
                    entry.Modified = info.LastWriteTimeUtc;
                    entry.Accessed = info.LastAccessTimeUtc;
                    entry.Created = info.CreationTimeUtc;
                }
                else
                {
                    var directory = new DirectoryInfo(entry.Path);
                    entry.Modified = directory.LastWriteTimeUtc;
                    entry.Accessed = directory.LastAccessTimeUtc;
                    entry.Created = directory.CreationTimeUtc;
                }
            }
            catch (Exception)
            {
                // 搜索期间文件被删除/无权限：保留路径，元数据留空。
                entry.Exists = false;
            }
        }

        private static bool SortNeedsMetadata(EverythingSort value)
        {
            switch (value)
            {
                case EverythingSort.SizeAscending:
                case EverythingSort.SizeDescending:
                case EverythingSort.DateCreatedAscending:
                case EverythingSort.DateCreatedDescending:
                case EverythingSort.DateModifiedAscending:
                case EverythingSort.DateModifiedDescending:
                case EverythingSort.DateAccessedAscending:
                case EverythingSort.DateAccessedDescending:
                case EverythingSort.DateRunAscending:
                case EverythingSort.DateRunDescending:
                case EverythingSort.DateRecentlyChangedAscending:
                case EverythingSort.DateRecentlyChangedDescending:
                case EverythingSort.AttributesAscending:
                case EverythingSort.AttributesDescending:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDescending(EverythingSort value)
        {
            switch (value)
            {
                case EverythingSort.NameDescending:
                case EverythingSort.PathDescending:
                case EverythingSort.SizeDescending:
                case EverythingSort.ExtensionDescending:
                case EverythingSort.TypeNameDescending:
                case EverythingSort.DateCreatedDescending:
                case EverythingSort.DateModifiedDescending:
                case EverythingSort.AttributesDescending:
                case EverythingSort.FileListFileNameDescending:
                case EverythingSort.RunCountDescending:
                case EverythingSort.DateRecentlyChangedDescending:
                case EverythingSort.DateAccessedDescending:
                case EverythingSort.DateRunDescending:
                    return true;
                default:
                    return false;
            }
        }

        private static int CompareEntries(Entry left, Entry right, EverythingSort sortValue)
        {
            int comparison;
            switch (sortValue)
            {
                case EverythingSort.PathAscending:
                case EverythingSort.PathDescending:
                    comparison = string.CompareOrdinal(left.Path, right.Path);
                    break;
                case EverythingSort.SizeAscending:
                case EverythingSort.SizeDescending:
                    comparison = left.Size.CompareTo(right.Size);
                    break;
                case EverythingSort.ExtensionAscending:
                case EverythingSort.ExtensionDescending:
                case EverythingSort.TypeNameAscending:
                case EverythingSort.TypeNameDescending:
                    comparison = string.Compare(left.Extension, right.Extension, StringComparison.OrdinalIgnoreCase);
                    break;
                case EverythingSort.DateCreatedAscending:
                case EverythingSort.DateCreatedDescending:
                    comparison = CompareDates(left.Created, right.Created);
                    break;
                case EverythingSort.DateModifiedAscending:
                case EverythingSort.DateModifiedDescending:
                    comparison = CompareDates(left.Modified, right.Modified);
                    break;
                case EverythingSort.DateAccessedAscending:
                case EverythingSort.DateAccessedDescending:
                    comparison = CompareDates(left.Accessed, right.Accessed);
                    break;
                case EverythingSort.AttributesAscending:
                case EverythingSort.AttributesDescending:
                    comparison = left.Attributes.CompareTo(right.Attributes);
                    break;
                default:
                    comparison = string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            if (comparison == 0)
            {
                comparison = string.CompareOrdinal(left.Path, right.Path);
            }

            return IsDescending(sortValue) ? -comparison : comparison;
        }

        private static int CompareDates(DateTime? left, DateTime? right)
        {
            if (left.HasValue && right.HasValue)
            {
                return left.Value.CompareTo(right.Value);
            }
            if (left.HasValue)
            {
                return 1;
            }
            return right.HasValue ? -1 : 0;
        }
    }
}

#endif
