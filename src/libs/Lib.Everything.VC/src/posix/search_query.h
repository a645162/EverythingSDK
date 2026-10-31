/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_query.h
 * Brief: Everything 搜索语法在 POSIX 后端的可移植子集（解析与匹配）。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include <regex>
#include <string>
#include <vector>

namespace Lib
{
namespace Everything
{
namespace Posix
{
struct SearchTerm
{
    enum class Kind
    {
        Substring,
        Glob,
        Regex,
        Extension,
        PathContains,
        FileNameContains
    };

    Kind kind{Kind::Substring};
    bool negated{false};
    std::string text;
    std::vector<std::string> extensions;

    // 正则只编译一次，避免每个候选路径都重新解析。
    bool regex_valid{false};
    std::regex regex_value;
};

struct SearchQuery
{
    std::vector<SearchTerm> terms;
    bool match_path{false};
    bool match_case{false};
    bool match_whole_word{false};
    bool use_regex{false};

    bool empty() const noexcept { return terms.empty(); }
    bool matches(const std::string& full_path) const;
};

// 支持的语法子集（其余 Everything 高级过滤词会被当成普通文本）：
//   ext:png;jpg        扩展名集合
//   path:"Program Files"  完整路径包含
//   file:"IMG_0001"    文件名包含
//   "quoted phrase"    短语
//   !term              排除
//   term               普通文本（MatchPath 决定匹配文件名还是完整路径）
SearchQuery ParseSearchQuery(const std::string& search_text, bool match_path, bool match_case, bool match_whole_word,
                             bool use_regex);

std::string AsciiLower(const std::string& text);
std::string BaseName(const std::string& path);
std::string PathExtension(const std::string& path);

// Everything 的默认通配符：* 任意长度、? 单个字符；[ ] 不作特殊处理，
// 避免与文件名字面量冲突。
bool GlobMatch(const std::string& pattern, const std::string& text, bool match_case);
} // namespace Posix
} // namespace Everything
} // namespace Lib
