// File: EverythingNativePosixQuery.cs
// Brief: POSIX 后端查询语法：解析、匹配与通配符。
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
        private enum TermKind
        {
            Substring,
            Glob,
            Regex,
            Extension,
            PathContains,
            FileNameContains
        }

        private sealed class Term
        {
            public TermKind Kind;
            public bool Negated;
            public string Text = string.Empty;
            public List<string> Extensions = new List<string>();
            public Regex Compiled;
        }

        private sealed class ParsedQuery
        {
            public readonly List<Term> Terms = new List<Term>();
            public bool MatchPath;
            public bool MatchCase;
            public bool MatchWholeWord;

            public bool Matches(string fullPath)
            {
                if (Terms.Count == 0)
                {
                    return true;
                }

                string name = BaseName(fullPath);
                foreach (Term term in Terms)
                {
                    bool matched;
                    switch (term.Kind)
                    {
                        case TermKind.Substring:
                            matched = Contains(MatchPath ? fullPath : name, term.Text);
                            break;
                        case TermKind.Glob:
                            matched = GlobMatch(term.Text, MatchPath ? fullPath : name, MatchCase);
                            break;
                        case TermKind.Regex:
                            matched = term.Compiled != null
                                ? term.Compiled.IsMatch(MatchPath ? fullPath : name)
                                : Contains(MatchPath ? fullPath : name, term.Text);
                            break;
                        case TermKind.Extension:
                            matched = MatchesExtension(fullPath, term.Extensions);
                            break;
                        case TermKind.PathContains:
                            matched = Contains(fullPath, term.Text);
                            break;
                        case TermKind.FileNameContains:
                            matched = Contains(name, term.Text);
                            break;
                        default:
                            matched = false;
                            break;
                    }

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
