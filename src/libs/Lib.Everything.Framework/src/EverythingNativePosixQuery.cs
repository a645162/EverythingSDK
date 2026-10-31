// File: EverythingNativePosixQuery.cs
// Brief: POSIX 后端查询语法：解析、匹配与通配符。
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-10-31
//
// 说明：该文件仅在非 .NET Framework 目标中参与编译
// （netstandard2.0 / net6.0 / net8.0 / net10.0）；
// .NET Framework 目标是 Windows 专用，不需要也不应该编译本后端。

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Lib.Everything
{
    internal sealed partial class EverythingNativePosix
    {
        // ------------------------------------------------------------------
        // 查询语法
        // ------------------------------------------------------------------
        // 大小与时间的比较方式；区间用 Equal + HasUpper 表示（闭区间）。
        private enum CompareOp
        {
            Less,
            LessEqual,
            Equal,
            GreaterEqual,
            Greater
        }

        private enum TermKind
        {
            Substring,
            Glob,
            Regex,
            Extension,
            PathContains,
            FileNameContains,
            // 以下词条必须读到文件元数据（大小/时间）或文件内容才能判定，
            // 粗筛阶段（只有路径可用）会被跳过。
            Size,
            DateCreated,
            DateModified,
            DateAccessed,
            Content
        }

        private sealed class Term
        {
            public TermKind Kind;
            public bool Negated;
            public string Text = string.Empty;
            public List<string> Extensions = new List<string>();
            public Regex Compiled;

            // Size 用字节数，日期用 Unix 秒。
            public CompareOp Compare = CompareOp.Equal;
            public long Value;
            public bool HasUpper;
            public long Upper;
        }

        private sealed class ParsedQuery
        {
            public readonly List<Term> Terms = new List<Term>();
            public bool MatchPath;
            public bool MatchCase;
            public bool MatchWholeWord;

            public bool NeedsMetadata
            {
                get
                {
                    foreach (Term term in Terms)
                    {
                        if (TermNeedsMetadata(term.Kind))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }

            // 只有路径可用时的匹配：大小/时间/内容词条被跳过（视为不限制），
            // 这样 locate/fd/find/scan 的粗筛不会为了 stat 每个文件而拖慢发现阶段。
            public bool Matches(string fullPath)
            {
                return MatchesCore(fullPath, null);
            }

            // 拿到元数据后的匹配：真正判定大小/时间/内容词条。
            public bool Matches(Entry metadata)
            {
                return MatchesCore(metadata.Path, metadata);
            }

            private bool MatchesCore(string fullPath, Entry metadata)
            {
                if (Terms.Count == 0)
                {
                    return true;
                }

                string name = BaseName(fullPath);
                foreach (Term term in Terms)
                {
                    // 必须在取反之前跳过——把它们当 true 再取反会变成 false，
                    // 候选会被全部误杀。
                    if (metadata == null && TermNeedsMetadata(term.Kind))
                    {
                        continue;
                    }

                    bool matched = EvaluateTerm(term, fullPath, name, metadata);
                    if (term.Negated)
                    {
                        matched = !matched;
                    }
                    if (!matched)
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool EvaluateTerm(Term term, string fullPath, string name, Entry metadata)
            {
                switch (term.Kind)
                {
                    case TermKind.Substring:
                        return Contains(MatchPath ? fullPath : name, term.Text);
                    case TermKind.Glob:
                        return GlobMatch(term.Text, MatchPath ? fullPath : name, MatchCase);
                    case TermKind.Regex:
                        return term.Compiled != null
                            ? term.Compiled.IsMatch(MatchPath ? fullPath : name)
                            : Contains(MatchPath ? fullPath : name, term.Text);
                    case TermKind.Extension:
                        return MatchesExtension(fullPath, term.Extensions);
                    case TermKind.PathContains:
                        return Contains(fullPath, term.Text);
                    case TermKind.FileNameContains:
                        return Contains(name, term.Text);
                    case TermKind.Size:
                    {
                        // 与 Everything 一致：目录大小按 0 处理（不拿 inode 的
                        // 目录项大小冒充），否则 size:>1kb 会把几乎所有目录都算进去。
                        long size = metadata.IsFile ? metadata.Size : 0L;
                        return MatchesNumber(term, size);
                    }
                    case TermKind.DateCreated:
                        return metadata.Created.HasValue &&
                               MatchesNumber(term, ToUnixSeconds(metadata.Created.Value));
                    case TermKind.DateModified:
                        return metadata.Modified.HasValue &&
                               MatchesNumber(term, ToUnixSeconds(metadata.Modified.Value));
                    case TermKind.DateAccessed:
                        return metadata.Accessed.HasValue &&
                               MatchesNumber(term, ToUnixSeconds(metadata.Accessed.Value));
                    case TermKind.Content:
                        return metadata.IsFile &&
                               FileContainsText(fullPath, term.Text);
                    default:
                        return false;
                }
            }

            private bool Contains(string haystack, string needle)
            {
                if (string.IsNullOrEmpty(needle))
                {
                    return true;
                }

                StringComparison comparison = MatchCase
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                int position = haystack.IndexOf(needle, comparison);
                while (position >= 0)
                {
                    if (!MatchWholeWord)
                    {
                        return true;
                    }

                    bool leftOk = position == 0 || !IsWordCharacter(haystack[position - 1]);
                    int after = position + needle.Length;
                    bool rightOk = after >= haystack.Length || !IsWordCharacter(haystack[after]);
                    if (leftOk && rightOk)
                    {
                        return true;
                    }
                    position = haystack.IndexOf(needle, position + 1, comparison);
                }
                return false;
            }

            private bool MatchesExtension(string fullPath, List<string> extensions)
            {
                string extension = PathExtension(fullPath);
                if (extension.Length == 0)
                {
                    return false;
                }

                StringComparison comparison = MatchCase
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;
                foreach (string expected in extensions)
                {
                    if (string.Equals(extension, expected, comparison))
                    {
                        return true;
                    }
                }
                return false;
            }

            // content: 只读文件开头的一段。整文件读取会让一次搜索变成「把候选文件
            // 全读一遍」，与路径索引后端的设计目标冲突；文档里说明这是前缀匹配。
            private const int ContentProbeBytes = 256 * 1024;

            private bool FileContainsText(string path, string needle)
            {
                if (needle.Length == 0)
                {
                    return true;
                }

                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                    {
                        var buffer = new byte[ContentProbeBytes];
                        int read = 0;
                        int chunk;
                        while (read < buffer.Length &&
                               (chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
                        {
                            read += chunk;
                        }

                        var haystack = new byte[read];
                        Buffer.BlockCopy(buffer, 0, haystack, 0, read);

                        byte[] target = Encoding.UTF8.GetBytes(needle);
                        if (!MatchCase)
                        {
                            AsciiLowerInPlace(haystack);
                            AsciiLowerInPlace(target);
                        }
                        return IndexOfBytes(haystack, target) >= 0;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
        }

        // 只折叠 ASCII 大小写，与 C++ 侧的 AsciiLower 保持一致：
        // 非 ASCII 字节原样保留，不引入与平台相关的文化大小写规则。
        private static void AsciiLowerInPlace(byte[] value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] >= (byte)'A' && value[index] <= (byte)'Z')
                {
                    value[index] = (byte)(value[index] + 32);
                }
            }
        }

        private static int IndexOfBytes(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0)
            {
                return 0;
            }
            int last = haystack.Length - needle.Length;
            for (int start = 0; start <= last; start++)
            {
                int offset = 0;
                while (offset < needle.Length && haystack[start + offset] == needle[offset])
                {
                    offset++;
                }
                if (offset == needle.Length)
                {
                    return start;
                }
            }
            return -1;
        }

        private static bool TermNeedsMetadata(TermKind kind)
        {
            switch (kind)
            {
                case TermKind.Size:
                case TermKind.DateCreated:
                case TermKind.DateModified:
                case TermKind.DateAccessed:
                case TermKind.Content:
                    return true;
                default:
                    return false;
            }
        }

        private static bool MatchesNumber(Term term, long actual)
        {
            if (term.HasUpper)
            {
                return actual >= term.Value && actual <= term.Upper;
            }

            switch (term.Compare)
            {
                case CompareOp.Less:
                    return actual < term.Value;
                case CompareOp.LessEqual:
                    return actual <= term.Value;
                case CompareOp.Greater:
                    return actual > term.Value;
                case CompareOp.GreaterEqual:
                    return actual >= term.Value;
                case CompareOp.Equal:
                    return actual == term.Value;
                default:
                    return false;
            }
        }

        private static long ToUnixSeconds(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
                : value.ToUniversalTime();
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static bool IsWordCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool GlobMatch(string pattern, string text, bool matchCase)
        {
            int patternIndex = 0;
            int textIndex = 0;
            int starPattern = -1;
            int starText = 0;

            while (textIndex < text.Length)
            {
                if (patternIndex < pattern.Length &&
                    (pattern[patternIndex] == '?' ||
                     EqualsCharacter(pattern[patternIndex], text[textIndex], matchCase)))
                {
                    patternIndex++;
                    textIndex++;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starPattern = patternIndex++;
                    starText = textIndex;
                }
                else if (starPattern >= 0)
                {
                    patternIndex = starPattern + 1;
                    textIndex = ++starText;
                }
                else
                {
                    return false;
                }
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }
            return patternIndex == pattern.Length;
        }

        private static bool EqualsCharacter(char left, char right, bool matchCase)
        {
            if (matchCase)
            {
                return left == right;
            }
            return char.ToLowerInvariant(left) == char.ToLowerInvariant(right);
        }

        private static readonly string[] DateFormats =
        {
            "yyyy-M-d",
            "yyyy-M-d H:m",
            "yyyy-M-d H:m:s"
        };

        // 解析 ">=", "<=", "=", ">", "<" 前缀；没有前缀时等于「精确匹配」。
        private static void ParseComparisonPrefix(string text, out CompareOp op, out string rest)
        {
            if (text.StartsWith(">=", StringComparison.Ordinal))
            {
                op = CompareOp.GreaterEqual;
                rest = text.Substring(2);
                return;
            }
            if (text.StartsWith("<=", StringComparison.Ordinal))
            {
                op = CompareOp.LessEqual;
                rest = text.Substring(2);
                return;
            }
            if (text.StartsWith("=", StringComparison.Ordinal))
            {
                op = CompareOp.Equal;
                rest = text.Substring(1);
                return;
            }
            if (text.StartsWith(">", StringComparison.Ordinal))
            {
                op = CompareOp.Greater;
                rest = text.Substring(1);
                return;
            }
            if (text.StartsWith("<", StringComparison.Ordinal))
            {
                op = CompareOp.Less;
                rest = text.Substring(1);
                return;
            }

            op = CompareOp.Equal;
            rest = text;
        }

        // 把 "1.5mb" / "1024" 这类写法换算成字节数。
        private static bool ParseSizeText(string text, out long bytes)
        {
            bytes = 0;
            string trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            int index = 0;
            while (index < trimmed.Length &&
                   (char.IsDigit(trimmed[index]) || trimmed[index] == '.'))
            {
                index++;
            }
            if (index == 0)
            {
                return false;
            }

            double amount;
            if (!double.TryParse(trimmed.Substring(0, index), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out amount) || amount < 0.0)
            {
                return false;
            }

            string unit = trimmed.Substring(index).Trim().ToLowerInvariant();
            double multiplier;
            switch (unit)
            {
                case "":
                case "b":
                    multiplier = 1.0;
                    break;
                case "k":
                case "kb":
                    multiplier = 1024.0;
                    break;
                case "m":
                case "mb":
                    multiplier = 1024.0 * 1024.0;
                    break;
                case "g":
                case "gb":
                    multiplier = 1024.0 * 1024.0 * 1024.0;
                    break;
                case "t":
                case "tb":
                    multiplier = 1024.0 * 1024.0 * 1024.0 * 1024.0;
                    break;
                default:
                    return false;
            }

            double value = amount * multiplier;
            // 超过 long 范围的输入直接判为非法，不要溢出成负数。
            if (value > 9.0e18)
            {
                return false;
            }
            bytes = (long)(value + 0.5);
            return true;
        }

        // 解析 "2024-01-31" / "2024/01/31" / "2024-01-31 12:30[:00]"。dayOnly
        // 为真表示输入里没有时分秒，调用方据此按「整天」处理。
        private static bool ParseDateText(string text, out long seconds, out bool dayOnly)
        {
            seconds = 0;
            dayOnly = false;

            string normalized = text.Trim().Replace('/', '-');
            if (normalized.Length == 0)
            {
                return false;
            }

            DateTime parsed;
            if (DateTime.TryParseExact(normalized, DateFormats[0], CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed))
            {
                dayOnly = true;
            }
            else if (!DateTime.TryParseExact(normalized, DateFormats[1], CultureInfo.InvariantCulture,
                         DateTimeStyles.None, out parsed) &&
                     !DateTime.TryParseExact(normalized, DateFormats[2], CultureInfo.InvariantCulture,
                         DateTimeStyles.None, out parsed))
            {
                return false;
            }

            // 按本地时区解释，和用户看到的文件时间一致。
            seconds = ToUnixSeconds(parsed);
            return true;
        }

        private static bool ParseSizeTerm(string body, Term term)
        {
            CompareOp op;
            string rest;
            ParseComparisonPrefix(body.Trim(), out op, out rest);

            int range = rest.IndexOf("..", StringComparison.Ordinal);
            if (range >= 0)
            {
                long lower;
                long upper;
                if (!ParseSizeText(rest.Substring(0, range), out lower) ||
                    !ParseSizeText(rest.Substring(range + 2), out upper))
                {
                    return false;
                }
                if (upper < lower)
                {
                    long swap = lower;
                    lower = upper;
                    upper = swap;
                }
                term.Compare = CompareOp.Equal;
                term.Value = lower;
                term.HasUpper = true;
                term.Upper = upper;
                return true;
            }

            long value;
            if (!ParseSizeText(rest, out value))
            {
                return false;
            }
            term.Compare = op;
            term.Value = value;
            return true;
        }

        private static bool ParseDateTerm(string body, Term term)
        {
            CompareOp op;
            string rest;
            ParseComparisonPrefix(body.Trim(), out op, out rest);

            int range = rest.IndexOf("..", StringComparison.Ordinal);
            if (range >= 0)
            {
                long lower;
                long upper;
                bool lowerDayOnly;
                bool upperDayOnly;
                if (!ParseDateText(rest.Substring(0, range), out lower, out lowerDayOnly) ||
                    !ParseDateText(rest.Substring(range + 2), out upper, out upperDayOnly))
                {
                    return false;
                }
                if (upperDayOnly)
                {
                    upper += 86399;
                }
                if (upper < lower)
                {
                    long swap = lower;
                    lower = upper;
                    upper = swap;
                }
                term.Compare = CompareOp.Equal;
                term.Value = lower;
                term.HasUpper = true;
                term.Upper = upper;
                return true;
            }

            long value;
            bool dayOnly;
            if (!ParseDateText(rest, out value, out dayOnly))
            {
                return false;
            }
            term.Compare = op;
            term.Value = value;
            // 只写日期时按「整天」理解：dm:2024-01-01 就是那一天的任意时刻。
            if (dayOnly && op == CompareOp.Equal)
            {
                term.HasUpper = true;
                term.Upper = value + 86399;
            }
            return true;
        }

        private ParsedQuery ParseQuery()
        {
            var query = new ParsedQuery
            {
                MatchPath = matchPath,
                MatchCase = matchCase,
                MatchWholeWord = matchWholeWord
            };

            foreach (string token in Tokenize(searchText))
            {
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                var term = new Term();
                string body = token;
                if (body.Length > 1 && body[0] == '!')
                {
                    term.Negated = true;
                    body = body.Substring(1);
                }
                if (body.Length == 0)
                {
                    continue;
                }

                if (StartsWithIgnoreCase(body, "ext:"))
                {
                    term.Kind = TermKind.Extension;
                    foreach (string value in SplitExtensions(body.Substring(4)))
                    {
                        term.Extensions.Add(value);
                    }
                    if (term.Extensions.Count > 0)
                    {
                        query.Terms.Add(term);
                    }
                    continue;
                }

                if (StartsWithIgnoreCase(body, "size:"))
                {
                    if (ParseSizeTerm(body.Substring(5), term))
                    {
                        term.Kind = TermKind.Size;
                        query.Terms.Add(term);
                        continue;
                    }
                    // 解析失败就退化成普通文本（例如用户就是想找名字里含 "size:x" 的文件）。
                }

                if (StartsWithIgnoreCase(body, "dc:") &&
                    ParseDateTerm(body.Substring(3), term))
                {
                    term.Kind = TermKind.DateCreated;
                    query.Terms.Add(term);
                    continue;
                }
                if (StartsWithIgnoreCase(body, "datecreated:") &&
                    ParseDateTerm(body.Substring(12), term))
                {
                    term.Kind = TermKind.DateCreated;
                    query.Terms.Add(term);
                    continue;
                }
                if (StartsWithIgnoreCase(body, "dm:") &&
                    ParseDateTerm(body.Substring(3), term))
                {
                    term.Kind = TermKind.DateModified;
                    query.Terms.Add(term);
                    continue;
                }
                if (StartsWithIgnoreCase(body, "datemodified:") &&
                    ParseDateTerm(body.Substring(13), term))
                {
                    term.Kind = TermKind.DateModified;
                    query.Terms.Add(term);
                    continue;
                }
                if (StartsWithIgnoreCase(body, "da:") &&
                    ParseDateTerm(body.Substring(3), term))
                {
                    term.Kind = TermKind.DateAccessed;
                    query.Terms.Add(term);
                    continue;
                }
                if (StartsWithIgnoreCase(body, "dateaccessed:") &&
                    ParseDateTerm(body.Substring(13), term))
                {
                    term.Kind = TermKind.DateAccessed;
                    query.Terms.Add(term);
                    continue;
                }

                if (StartsWithIgnoreCase(body, "content:"))
                {
                    term.Kind = TermKind.Content;
                    term.Text = body.Substring(8).Trim();
                    if (term.Text.Length > 0)
                    {
                        query.Terms.Add(term);
                    }
                    continue;
                }

                if (StartsWithIgnoreCase(body, "path:"))
                {
                    term.Kind = TermKind.PathContains;
                    term.Text = body.Substring(5).Trim();
                    if (term.Text.Length > 0)
                    {
                        query.Terms.Add(term);
                    }
                    continue;
                }

                if (StartsWithIgnoreCase(body, "file:"))
                {
                    term.Kind = TermKind.FileNameContains;
                    term.Text = body.Substring(5).Trim();
                    if (term.Text.Length > 0)
                    {
                        query.Terms.Add(term);
                    }
                    continue;
                }

                term.Text = body;
                if (useRegex)
                {
                    term.Kind = TermKind.Regex;
                    try
                    {
                        var options = RegexOptions.CultureInvariant;
                        if (!matchCase)
                        {
                            options |= RegexOptions.IgnoreCase;
                        }
                        term.Compiled = new Regex(body, options, TimeSpan.FromSeconds(2));
                    }
                    catch (ArgumentException)
                    {
                        term.Compiled = null;
                    }
                }
                else
                {
                    // Everything 默认支持通配符；含 * 或 ? 的 token 按整串 glob 匹配。
                    term.Kind = body.IndexOf('*') >= 0 || body.IndexOf('?') >= 0
                        ? TermKind.Glob
                        : TermKind.Substring;
                }
                query.Terms.Add(term);
            }

            return query;
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            bool hasContent = false;

            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (value == '"')
                {
                    if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    hasContent = true;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(value))
                {
                    if (hasContent)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        hasContent = false;
                    }
                    continue;
                }

                current.Append(value);
                hasContent = true;
            }

            if (hasContent)
            {
                tokens.Add(current.ToString());
            }
            return tokens;
        }

        private static List<string> SplitExtensions(string value)
        {
            var extensions = new List<string>();
            foreach (string raw in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string extension = raw.Trim();
                if (extension.StartsWith(".", StringComparison.Ordinal))
                {
                    extension = extension.Substring(1);
                }
                if (extension.Length > 0)
                {
                    extensions.Add(extension);
                }
            }
            return extensions;
        }

        private static bool StartsWithIgnoreCase(string text, string prefix)
        {
            return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string BaseName(string path)
        {
            return Path.GetFileName(path);
        }

        private static string PathExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Length > 0 ? extension.Substring(1) : string.Empty;
        }
    }
}

#endif
