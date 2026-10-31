// File: EverythingNativePosixBackend.cs
// Brief: POSIX 后端探测与候选路径枚举（locate → fd → find → 进程内扫描）。
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
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Lib.Everything
{
    internal sealed partial class EverythingNativePosix
    {
        // ------------------------------------------------------------------
        // 候选路径收集
        // ------------------------------------------------------------------
        private enum BackendKind
        {
            None,
            Locate,
            Fd,
            Find,
            Scan
        }

        private sealed class BackendInfo
        {
            public BackendKind Kind;
            public string Executable;
            public bool DatabaseLoaded;
            public string Reason;
            public string Name => Kind == BackendKind.Locate ? "locate"
                                : Kind == BackendKind.Fd ? "fd"
                                : Kind == BackendKind.Find ? "find"
                                : "scan";
        }

        private static readonly BackendKind[] AllBackends =
        {
            BackendKind.Locate,
            BackendKind.Fd,
            BackendKind.Find,
            BackendKind.Scan
        };

        // 手动指定的后端：空字符串表示自动。搜索跑在调用方的工作线程上，
        // 因此用 volatile 保证写入后其它线程能立刻看到新值。
        private static volatile string preferredBackend = string.Empty;

        private static string PreferredBackendName() => preferredBackend ?? string.Empty;

        private static void SetPreferredBackendName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                preferredBackend = string.Empty;
                return;
            }

            // 无法识别的名字不写入，避免把用户的输入静默变成「永远自动」。
            BackendKind kind;
            if (TryParseBackendName(name, out kind))
            {
                preferredBackend = BackendNameOf(kind);
            }
        }

        private static bool TryParseBackendName(string name, out BackendKind kind)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "locate":
                    kind = BackendKind.Locate;
                    return true;
                case "fd":
                    kind = BackendKind.Fd;
                    return true;
                case "find":
                    kind = BackendKind.Find;
                    return true;
                case "scan":
                    kind = BackendKind.Scan;
                    return true;
            }

            kind = BackendKind.None;
            return false;
        }

        private static string BackendNameOf(BackendKind kind)
        {
            return new BackendInfo { Kind = kind }.Name;
        }

        private string[] BackendNames()
        {
            var names = new List<string>();
            foreach (BackendKind kind in AllBackends)
            {
                if (IsAvailable(ResolveBackendKind(kind)))
                {
                    names.Add(BackendNameOf(kind));
                }
            }
            return names.ToArray();
        }

        private BackendInfo ResolveBackend()
        {
            // 刻意不做缓存：探测只是几次 stat()，而缓存会让环境变量覆盖在
            // 同一进程内完全不生效（测试和诊断场景都需要能立刻切换）。

            // 1) 显式 API 偏好优先，便于界面让用户手动指定后端。
            string preferred = preferredBackend;
            BackendKind preferredKind;
            if (!string.IsNullOrEmpty(preferred) && TryParseBackendName(preferred, out preferredKind))
            {
                BackendInfo forced = FirstAvailableFrom(FallbackOrder(preferredKind));
                forced.Reason += " (preferred)";
                return forced;
            }

            // 2) 环境变量，保留无条件脚本化能力。
            string forcedName = Environment.GetEnvironmentVariable("EVERYTHING_SDK_SEARCH_BACKEND") ?? string.Empty;
            BackendKind envKind;
            if (TryParseBackendName(forcedName, out envKind))
            {
                BackendInfo forced = FirstAvailableFrom(FallbackOrder(envKind));
                forced.Reason += " (EVERYTHING_SDK_SEARCH_BACKEND)";
                return forced;
            }

            // 3) 自动：locate → fd → find → scan。
            return FirstAvailableFrom(FallbackOrder(BackendKind.Locate));
        }

        /// <summary>构造指定后端的探测结果；Executable 为 null 表示本机不可用。</summary>
        private static BackendInfo ResolveBackendKind(BackendKind kind)
        {
            var info = new BackendInfo { Kind = kind };

            switch (kind)
            {
                case BackendKind.Locate:
                {
                    string database = FirstExistingDatabase();
                    if (database == null)
                    {
                        info.Reason = "locate database missing";
                        return info;
                    }

                    string plocate = FindExecutable("plocate");
                    if (plocate != null)
                    {
                        info.Executable = plocate;
                        info.DatabaseLoaded = true;
                        info.Reason = "plocate database " + database;
                        return info;
                    }

                    string locate = FindExecutable("locate");
                    if (locate != null)
                    {
                        info.Executable = locate;
                        info.DatabaseLoaded = true;
                        info.Reason = "locate database " + database;
                        return info;
                    }

                    info.Reason = "locate executable missing";
                    return info;
                }
                case BackendKind.Fd:
                {
                    // Debian/Ubuntu 的包名是 fd-find，可执行文件叫 fdfind，
                    // 因此两个名字都要探测。
                    string fd = FindExecutable("fd");
                    string fdfind = FindExecutable("fdfind");
                    info.Executable = fd ?? fdfind;
                    info.Reason = info.Executable == null
                        ? "fd executable missing"
                        : "fd executable " + info.Executable;
                    return info;
                }
                case BackendKind.Find:
                {
                    info.Executable = FindExecutable("find");
                    info.Reason = info.Executable == null
                        ? "find executable missing"
                        : "find executable " + info.Executable;
                    return info;
                }
                default:
                    info.Reason = "in-process scan";
                    return info;
            }
        }

        private static bool IsAvailable(BackendInfo info)
        {
            return info.Kind == BackendKind.Scan || !string.IsNullOrEmpty(info.Executable);
        }

        // 显式指定某个后端后，不可用时要按什么顺序回退。
        // 指定 fd/find 时跳过 locate：索引可能过期，用户点名了就别用旧快照。
        private static BackendKind[] FallbackOrder(BackendKind forced)
        {
            switch (forced)
            {
                case BackendKind.Fd:
                    return new[] { BackendKind.Fd, BackendKind.Find, BackendKind.Scan };
                case BackendKind.Find:
                    return new[] { BackendKind.Find, BackendKind.Scan };
                case BackendKind.Scan:
                    return new[] { BackendKind.Scan };
                default:
                    return new[] { BackendKind.Locate, BackendKind.Fd, BackendKind.Find, BackendKind.Scan };
            }
        }

        private static BackendInfo FirstAvailableFrom(IEnumerable<BackendKind> order)
        {
            foreach (BackendKind candidate in order)
            {
                BackendInfo info = ResolveBackendKind(candidate);
                if (IsAvailable(info))
                {
                    return info;
                }
            }

            // 理论上到不了这里：FallbackOrder 的最后一项一定是 Scan。
            return ResolveBackendKind(BackendKind.Scan);
        }

        private static string FindExecutable(string name)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (string directory in path.Split(':'))
            {
                if (directory.Length == 0)
                {
                    continue;
                }
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static string FirstExistingDatabase()
        {
            var candidates = new List<string>();
            string configured = Environment.GetEnvironmentVariable("EVERYTHING_SDK_LOCATE_DB");
            if (!string.IsNullOrEmpty(configured))
            {
                candidates.Add(configured);
            }
            candidates.Add("/var/lib/plocate/plocate.db");
            candidates.Add("/var/lib/mlocate/mlocate.db");
            candidates.Add("/var/lib/locatedb");
            candidates.Add("/var/cache/locate/locatedb");
            candidates.Add("/usr/var/locatedb");

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static List<string> SearchRoots()
        {
            var roots = new List<string>();
            string configured = Environment.GetEnvironmentVariable("EVERYTHING_SDK_SEARCH_ROOTS");
            if (!string.IsNullOrEmpty(configured))
            {
                foreach (string raw in configured.Split(':'))
                {
                    string root = raw.TrimEnd('/');
                    if (root.Length > 0)
                    {
                        roots.Add(root);
                    }
                }
                if (roots.Count > 0)
                {
                    return roots;
                }
            }

            string home = Environment.GetEnvironmentVariable("HOME");
            roots.Add(string.IsNullOrEmpty(home) ? "/" : home);
            return roots;
        }

        private int EffectiveCandidateCap()
        {
            int cap = DefaultCandidateCap;
            string configured = Environment.GetEnvironmentVariable("EVERYTHING_SDK_MAX_CANDIDATES");
            if (!string.IsNullOrEmpty(configured) &&
                int.TryParse(configured, out int parsed) && parsed > 0)
            {
                cap = parsed;
            }

            if (max > 0)
            {
                cap = (int)Math.Max((ulong)cap, (ulong)offset + max);
            }
            return cap;
        }

        private List<string> CollectCandidates(ParsedQuery query, int cap)
        {
            BackendInfo info = ResolveBackend();
            switch (info.Kind)
            {
                case BackendKind.Locate:
                    return CollectFromLocate(info, query, cap);
                case BackendKind.Fd:
                    return CollectFromFd(info, query, cap);
                case BackendKind.Find:
                    return CollectFromFind(query, cap);
                default:
                    return CollectFromScan(query, cap);
            }
        }

        private List<string> CollectFromLocate(BackendInfo info, ParsedQuery query, int cap)
        {
            var paths = new List<string>();
            List<string> patterns = LocatePatterns(query);
            List<string> extensions = ExtensionGlobs(query);
            List<string> roots = RootsFromEnvironment();

            bool Consume(string record)
            {
                if (paths.Count >= cap)
                {
                    return false;
                }
                if (record.Length == 0 || record[0] != '/' || !MatchesRoots(record, roots))
                {
                    return true;
                }
                paths.Add(record);
                return true;
            }

            bool RunOnce(IReadOnlyList<string> extra)
            {
                var arguments = new List<string> { "-i", "-0", "-l", cap.ToString() };
                arguments.AddRange(patterns);
                arguments.AddRange(extra);
                return RunProcess(info.Executable, arguments, '\0', Consume, out _, out _);
            }

            if (patterns.Count > 0 || extensions.Count == 0)
            {
                RunOnce(Array.Empty<string>());
            }
            else
            {
                // locate 的多 pattern 是 AND，而扩展名列表是 OR，只能逐个扩展名查询再合并。
                foreach (string glob in extensions)
                {
                    if (paths.Count >= cap)
                    {
                        break;
                    }
                    RunOnce(new[] { glob });
                }
            }

            paths.Sort(StringComparer.Ordinal);
            RemoveDuplicates(paths);
            return paths;
        }

        private List<string> CollectFromFd(BackendInfo info, ParsedQuery query, int cap)
        {
            var paths = new List<string>();
            List<string> roots = SearchRoots();

            var arguments = new List<string>
            {
                "-i", // 与 Everything 默认一致：大小写不敏感
                "-0", // NUL 分隔，文件名里可以有换行
                "-a", // 输出绝对路径
                "-H", // 包含隐藏项
                "-I"  // 不受 .gitignore/.ignore 影响
            };

            // 扩展名列表在 Everything 里是 OR，fd 的多个 -e 同样是 OR。
            foreach (Term term in query.Terms)
            {
                if (term.Negated || term.Kind != TermKind.Extension)
                {
                    continue;
                }
                foreach (string extension in term.Extensions)
                {
                    arguments.Add("-e");
                    arguments.Add(extension);
                }
            }

            // 根目录为 / 时跳过伪文件系统：--exclude 按相对路径/名字匹配，
            // 这些名字在真实数据目录里极少出现，误伤面很小。
            if (roots.Contains("/"))
            {
                foreach (string special in new[] { "proc", "sys", "dev", "run", "snap" })
                {
                    arguments.Add("--exclude");
                    arguments.Add(special);
                }
            }

            AppendFdPatternArguments(query, arguments);
            arguments.AddRange(roots);

            RunProcess(info.Executable, arguments, '\0', record =>
            {
                if (paths.Count >= cap)
                {
                    return false;
                }

                string path = TrimTrailingSlash(record);
                if (path.Length > 0)
                {
                    paths.Add(path);
                }
                return true;
            }, out _, out _);

            paths.Sort(StringComparer.Ordinal);
            RemoveDuplicates(paths);
            return paths;
        }

        // 只把「一定能覆盖所有可能命中项」的条件交给 fd 做粗筛，其余在进程内判定。
        // 例如 MatchPath 打开时的文本条件若当成文件名模式，会漏掉只出现在目录里的匹配。
        private static void AppendFdPatternArguments(ParsedQuery query, List<string> arguments)
        {
            foreach (Term term in query.Terms)
            {
                if (term.Negated)
                {
                    continue;
                }

                if (term.Kind == TermKind.FileNameContains)
                {
                    arguments.Add("-F");
                    arguments.Add(term.Text);
                    return;
                }
                if (term.Kind == TermKind.Substring && !query.MatchPath)
                {
                    arguments.Add("-F");
                    arguments.Add(term.Text);
                    return;
                }
                if (term.Kind == TermKind.Glob && !query.MatchPath)
                {
                    arguments.Add("-g");
                    arguments.Add(EscapeGlobKeepingWildcards(term.Text));
                    return;
                }
            }

            // 没有可用作粗筛的条件：fd 的 "." 匹配所有条目，由进程内匹配做最终判定。
            arguments.Add(".");
        }

        private static string TrimTrailingSlash(string path)
        {
            int end = path.Length;
            while (end > 1 && path[end - 1] == '/')
            {
                end--;
            }
            return path.Substring(0, end);
        }

        private List<string> CollectFromFind(ParsedQuery query, int cap)
        {
            var paths = new List<string>();
            List<string> roots = SearchRoots();

            BackendInfo info = ResolveBackend();
            var arguments = new List<string>();
            arguments.AddRange(roots);

            bool pruneSpecial = roots.Contains("/");
            if (pruneSpecial)
            {
                arguments.Add("(");
                string[] special = { "/proc", "/sys", "/dev", "/run", "/snap" };
                for (int index = 0; index < special.Length; index++)
                {
                    if (index > 0)
                    {
                        arguments.Add("-o");
                    }
                    arguments.Add("-path");
                    arguments.Add(special[index]);
                }
                arguments.Add(")");
                arguments.Add("-prune");
                arguments.Add("-o");
            }

            List<string> extensions = ExtensionGlobs(query);
            if (extensions.Count > 0)
            {
                arguments.Add("(");
                for (int index = 0; index < extensions.Count; index++)
                {
                    if (index > 0)
                    {
                        arguments.Add("-o");
                    }
                    arguments.Add("-ipath");
                    arguments.Add(extensions[index]);
                }
                arguments.Add(")");
            }

            foreach (string pattern in LocatePatterns(query))
            {
                arguments.Add("-ipath");
                arguments.Add(pattern);
            }
            arguments.Add("-print0");

            RunProcess(info.Executable, arguments, '\0', record =>
            {
                if (paths.Count >= cap)
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(record))
                {
                    paths.Add(record);
                }
                return true;
            }, out _, out _);

            RemoveDuplicates(paths);
            return paths;
        }

        private List<string> CollectFromScan(ParsedQuery query, int cap)
        {
            var paths = new List<string>();
            var pending = new Stack<string>(SearchRoots());

            while (pending.Count > 0 && paths.Count < cap)
            {
                string directory = pending.Pop();
                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(directory);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (string entry in entries)
                {
                    if (paths.Count >= cap)
                    {
                        break;
                    }

                    if (query.Matches(entry))
                    {
                        paths.Add(entry);
                    }

                    try
                    {
                        FileAttributes attributes = File.GetAttributes(entry);
                        bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                        bool isLink = (attributes & FileAttributes.ReparsePoint) != 0;
                        if (isDirectory && !isLink)
                        {
                            pending.Push(entry);
                        }
                    }
                    catch (Exception)
                    {
                        // 无权限或竞态删除：跳过该条目。
                    }
                }
            }

            return paths;
        }

        private static List<string> RootsFromEnvironment()
        {
            var roots = new List<string>();
            string configured = Environment.GetEnvironmentVariable("EVERYTHING_SDK_SEARCH_ROOTS");
            if (string.IsNullOrEmpty(configured))
            {
                return roots;
            }
            foreach (string raw in configured.Split(':'))
            {
                string root = raw.TrimEnd('/');
                if (root.Length > 0)
                {
                    roots.Add(root);
                }
            }
            return roots;
        }

        private static bool MatchesRoots(string path, List<string> roots)
        {
            if (roots.Count == 0)
            {
                return true;
            }
            foreach (string root in roots)
            {
                if (root == "/" || path == root ||
                    (path.StartsWith(root, StringComparison.Ordinal) &&
                     path.Length > root.Length && path[root.Length] == '/'))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<string> LocatePatterns(ParsedQuery query)
        {
            var patterns = new List<string>();
            foreach (Term term in query.Terms)
            {
                if (term.Negated)
                {
                    continue;
                }
                switch (term.Kind)
                {
                    case TermKind.Substring:
                    case TermKind.PathContains:
                    case TermKind.FileNameContains:
                        patterns.Add("*" + EscapeGlob(term.Text) + "*");
                        break;
                    case TermKind.Glob:
                        patterns.Add(EscapeGlobKeepingWildcards(term.Text));
                        break;
                }
            }
            return patterns;
        }

        private static List<string> ExtensionGlobs(ParsedQuery query)
        {
            var globs = new List<string>();
            foreach (Term term in query.Terms)
            {
                if (term.Negated || term.Kind != TermKind.Extension)
                {
                    continue;
                }
                foreach (string extension in term.Extensions)
                {
                    globs.Add("*." + EscapeGlob(extension));
                }
            }
            return globs;
        }

        private static string EscapeGlob(string text)
        {
            var builder = new StringBuilder(text.Length * 2);
            foreach (char value in text)
            {
                switch (value)
                {
                    case '*':
                        builder.Append("[*]");
                        break;
                    case '?':
                        builder.Append("[?]");
                        break;
                    case '[':
                        builder.Append("[[]");
                        break;
                    case ']':
                        builder.Append("[]]");
                        break;
                    case '\\':
                        builder.Append("[\\]");
                        break;
                    default:
                        builder.Append(value);
                        break;
                }
            }
            return builder.ToString();
        }

        private static string EscapeGlobKeepingWildcards(string text)
        {
            var builder = new StringBuilder(text.Length * 2);
            foreach (char value in text)
            {
                switch (value)
                {
                    case '[':
                        builder.Append("[[]");
                        break;
                    case ']':
                        builder.Append("[]]");
                        break;
                    case '\\':
                        builder.Append("[\\]");
                        break;
                    default:
                        builder.Append(value);
                        break;
                }
            }
            return builder.ToString();
        }

        private static void RemoveDuplicates(List<string> paths)
        {
            if (paths.Count < 2)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int write = 0;
            for (int read = 0; read < paths.Count; read++)
            {
                if (seen.Add(paths[read]))
                {
                    paths[write++] = paths[read];
                }
            }
            if (write < paths.Count)
            {
                paths.RemoveRange(write, paths.Count - write);
            }
        }

        private static bool RunProcess(
            string fileName,
            IReadOnlyList<string> arguments,
            char delimiter,
            Func<string, bool> consumer,
            out bool truncated,
            out string error)
        {
            truncated = false;
            error = null;

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
#if NETSTANDARD2_0
            // netstandard2.0 没有 ArgumentList，只能自己拼一个带引号的命令行；
            // .NET 在 Unix 上也是按同一套引号规则解析 Arguments 的。
            startInfo.Arguments = BuildArgumentString(arguments);
#else
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
#endif

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            if (process == null)
            {
                error = "failed to start " + fileName;
                return false;
            }

            try
            {
                var pending = new List<byte>(4096);
                var buffer = new byte[16384];
                bool stop = false;
                Stream stream = process.StandardOutput.BaseStream;

                while (!stop)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    for (int index = 0; index < read; index++)
                    {
                        byte value = buffer[index];
                        if (value == (byte)delimiter)
                        {
                            string record = Encoding.UTF8.GetString(pending.ToArray());
                            pending.Clear();
                            if (!consumer(record))
                            {
                                truncated = true;
                                stop = true;
                                break;
                            }
                        }
                        else
                        {
                            pending.Add(value);
                        }
                    }
                }

                if (!stop && pending.Count > 0)
                {
                    consumer(Encoding.UTF8.GetString(pending.ToArray()));
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }

            try
            {
                if (truncated && !process.HasExited)
                {
#if NETSTANDARD2_0
                    // netstandard2.0 没有 Kill(bool)：只能终止进程本身，
                    // 这里的目标是 fd/find，不会留下需要递归清理的子进程树。
                    process.Kill();
#else
                    process.Kill(true);
#endif
                }
                process.WaitForExit(5000);
            }
            catch (Exception)
            {
                // 进程已退出或已被终止。
            }

            return error == null;
        }

#if NETSTANDARD2_0
        // 按 .NET/Windows 的引号规则拼接命令行；Unix 上的 Process 也用同一套解析规则。
        private static string BuildArgumentString(IReadOnlyList<string> arguments)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < arguments.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                string argument = arguments[index];
                bool needsQuotes = argument.Length == 0 ||
                                   argument.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0;
                if (!needsQuotes)
                {
                    builder.Append(argument);
                    continue;
                }

                builder.Append('"');
                for (int position = 0; position < argument.Length; position++)
                {
                    int backslashes = 0;
                    while (position < argument.Length && argument[position] == '\\')
                    {
                        backslashes++;
                        position++;
                    }

                    if (position == argument.Length)
                    {
                        // 结尾反斜杠要翻倍，否则会把收尾引号转义掉。
                        builder.Append('\\', backslashes * 2);
                        break;
                    }

                    if (argument[position] == '"')
                    {
                        builder.Append('\\', backslashes * 2 + 1);
                    }
                    else
                    {
                        builder.Append('\\', backslashes);
                    }
                    builder.Append(argument[position]);
                }
                builder.Append('"');
            }
            return builder.ToString();
        }
#endif
    }
}

#endif
