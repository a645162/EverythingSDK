/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_query.h
 * Brief: Everything 搜索语法在 POSIX 后端的可移植子集（解析与匹配）。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include "file_metadata.h"

#include <cstdint>
#include <regex>
#include <string>
#include <vector>

namespace Lib {
namespace Everything {
namespace Posix {
// 大小与时间的比较方式。区间用 Equal + has_upper 表示（闭区间）。
enum class CompareOp { Less, LessEqual, Equal, GreaterEqual, Greater };

struct SearchTerm {
  enum class Kind {
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
  };

  Kind kind{Kind::Substring};
  bool negated{false};
  std::string text;
  std::vector<std::string> extensions;

  // Size 用字节数，日期用 Unix 秒。
  CompareOp compare{CompareOp::Equal};
  std::int64_t value{0};
  bool has_upper{false};
  std::int64_t upper{0};

  // 正则只编译一次，避免每个候选路径都重新解析。
  bool regex_valid{false};
  std::regex regex_value;
};

struct SearchQuery {
  std::vector<SearchTerm> terms;
  bool match_path{false};
  bool match_case{false};
  bool match_whole_word{false};
  bool use_regex{false};

  bool empty() const noexcept { return terms.empty(); }

  // 只有路径可用时的匹配：大小/时间/内容词条被跳过（视为不限制），
  // 这样 locate/fd/find/scan 的粗筛不会为了 stat 每个文件而拖慢发现阶段。
  bool matches(const std::string &full_path) const;

  // 拿到元数据后的匹配：真正判定大小/时间词条。
  bool matches(const std::string &full_path,
               const FileMetadata &metadata) const;

  // 是否需要读元数据（存在大小/时间/内容词条）。
  bool needs_metadata() const noexcept;
};

// 支持的语法子集（其余 Everything 高级过滤词会被当成普通文本）：
//   ext:png;jpg            扩展名集合
//   path:"Program Files"   完整路径包含
//   file:"IMG_0001"        文件名包含
//   size:>1mb              大小比较（支持 < <= = >= >，单位 b/kb/mb/gb/tb）
//   size:1mb..2gb          大小闭区间
//   dc:>=2024-01-01        创建时间比较（dc:/datecreated:）
//   dm:2024-01-01..2024-12-31  修改时间区间（dm:/datemodified:）
//   da:<2024-06-01         访问时间比较（da:/dateaccessed:）
//   content:"error"        文件内容包含（只读文件前 256 KiB）
//   "quoted phrase"        短语
//   !term                  排除
//   term                   普通文本（MatchPath 决定匹配文件名还是完整路径）
SearchQuery ParseSearchQuery(const std::string &search_text, bool match_path,
                             bool match_case, bool match_whole_word,
                             bool use_regex);

std::string AsciiLower(const std::string &text);
std::string BaseName(const std::string &path);
std::string PathExtension(const std::string &path);

// Everything 的默认通配符：* 任意长度、? 单个字符；[ ] 不作特殊处理，
// 避免与文件名字面量冲突。
bool GlobMatch(const std::string &pattern, const std::string &text,
               bool match_case);
} // namespace Posix
} // namespace Everything
} // namespace Lib
