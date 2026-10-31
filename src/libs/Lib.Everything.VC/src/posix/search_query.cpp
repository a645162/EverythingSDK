/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_query.cpp
 * Brief: Everything 搜索语法子集的解析与匹配实现。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#include "search_query.h"

#include <algorithm>
#include <cctype>
#include <cstddef>
#include <cstdio>
#include <cstdlib>
#include <ctime>
#include <iomanip>
#include <sstream>

namespace {
bool IsSpace(const char value) {
  return value == ' ' || value == '\t' || value == '\r' || value == '\n';
}

bool IsWordByte(const unsigned char value) {
  // 非 ASCII 字节视为单词字符，这样中文/重音字符不会被当成边界。
  return value >= 0x80U || std::isalnum(value) != 0 || value == '_';
}

std::vector<std::string> Tokenize(const std::string &text) {
  std::vector<std::string> tokens;
  std::string current;
  bool in_quotes = false;
  bool has_content = false;

  for (std::size_t index = 0; index < text.size(); ++index) {
    const char value = text[index];
    if (value == '"') {
      // 引号只负责分组，本身不进入 token。
      if (in_quotes && index + 1 < text.size() && text[index + 1] == '"') {
        current.push_back('"');
        ++index;
      } else {
        in_quotes = !in_quotes;
      }
      has_content = true;
      continue;
    }

    if (!in_quotes && IsSpace(value)) {
      if (has_content) {
        tokens.push_back(current);
        current.clear();
        has_content = false;
      }
      continue;
    }

    current.push_back(value);
    has_content = true;
  }

  if (has_content) {
    tokens.push_back(current);
  }

  return tokens;
}

std::string Trim(const std::string &text) {
  std::size_t begin = 0;
  while (begin < text.size() && IsSpace(text[begin])) {
    ++begin;
  }

  std::size_t end = text.size();
  while (end > begin && IsSpace(text[end - 1])) {
    --end;
  }

  return text.substr(begin, end - begin);
}

bool StartsWithIgnoreCase(const std::string &text, const std::string &prefix) {
  if (text.size() < prefix.size()) {
    return false;
  }

  for (std::size_t index = 0; index < prefix.size(); ++index) {
    if (std::tolower(static_cast<unsigned char>(text[index])) !=
        std::tolower(static_cast<unsigned char>(prefix[index]))) {
      return false;
    }
  }
  return true;
}

std::vector<std::string> SplitExtensions(const std::string &value) {
  std::vector<std::string> extensions;
  std::string current;
  for (std::size_t index = 0; index <= value.size(); ++index) {
    const bool at_end = index == value.size();
    const char character = at_end ? ';' : value[index];
    if (character == ';' || character == ',') {
      std::string extension = Trim(current);
      current.clear();
      if (extension.empty()) {
        continue;
      }
      if (extension.front() == '.') {
        extension.erase(extension.begin());
      }
      if (!extension.empty()) {
        extensions.push_back(extension);
      }
      continue;
    }
    current.push_back(character);
  }
  return extensions;
}

bool ContainsSubstring(const std::string &haystack, const std::string &needle,
                       const bool match_case, const bool match_whole_word) {
  if (needle.empty()) {
    return true;
  }

  const std::string lowered_haystack =
      match_case ? std::string() : Lib::Everything::Posix::AsciiLower(haystack);
  const std::string lowered_needle =
      match_case ? std::string() : Lib::Everything::Posix::AsciiLower(needle);
  const std::string &source = match_case ? haystack : lowered_haystack;
  const std::string &target = match_case ? needle : lowered_needle;

  std::size_t position = source.find(target);
  while (position != std::string::npos) {
    if (!match_whole_word) {
      return true;
    }

    const std::size_t before = position;
    const std::size_t after = position + target.size();
    const bool left_ok =
        before == 0 ||
        !IsWordByte(static_cast<unsigned char>(source[before - 1]));
    const bool right_ok =
        after >= source.size() ||
        !IsWordByte(static_cast<unsigned char>(source[after]));
    if (left_ok && right_ok) {
      return true;
    }

    position = source.find(target, position + 1);
  }

  return false;
}

bool EqualsCharacter(const char left, const char right, const bool match_case) {
  if (match_case) {
    return left == right;
  }

  return std::tolower(static_cast<unsigned char>(left)) ==
         std::tolower(static_cast<unsigned char>(right));
}
} // namespace

namespace Lib {
namespace Everything {
namespace Posix {
namespace {

// 把 "1.5mb" / "1024" 这类写法换算成字节数。
bool ParseSizeText(const std::string &text, std::int64_t *bytes) {
  const std::string trimmed = Trim(text);
  if (trimmed.empty()) {
    return false;
  }

  const char *begin = trimmed.c_str();
  char *end = nullptr;
  const double amount = std::strtod(begin, &end);
  if (end == begin || amount < 0.0) {
    return false;
  }

  const std::string unit = Trim(AsciiLower(std::string(end)));
  double multiplier = 0.0;
  if (unit.empty() || unit == "b") {
    multiplier = 1.0;
  } else if (unit == "k" || unit == "kb") {
    multiplier = 1024.0;
  } else if (unit == "m" || unit == "mb") {
    multiplier = 1024.0 * 1024.0;
  } else if (unit == "g" || unit == "gb") {
    multiplier = 1024.0 * 1024.0 * 1024.0;
  } else if (unit == "t" || unit == "tb") {
    multiplier = 1024.0 * 1024.0 * 1024.0 * 1024.0;
  } else {
    return false;
  }

  const double value = amount * multiplier;
  // 超过 int64 范围的输入直接判为非法，不要溢出成负数。
  if (value > 9.0e18) {
    return false;
  }
  *bytes = static_cast<std::int64_t>(value + 0.5);
  return true;
}

// 解析 "2024-01-31" / "2024/01/31" / "2024-01-31 12:30[:00]"。
// day_only 为真表示输入里没有时分秒，调用方据此按「整天」处理。
bool ParseDateText(const std::string &text, std::int64_t *seconds,
                   bool *day_only) {
  std::string normalized = Trim(text);
  if (normalized.empty()) {
    return false;
  }
  for (char &value : normalized) {
    if (value == '/') {
      value = '-';
    }
  }

  std::tm parsed{};
  std::istringstream stream(normalized);
  stream >> std::get_time(&parsed, "%Y-%m-%d");
  if (stream.fail()) {
    return false;
  }

  std::string rest;
  std::getline(stream, rest);
  rest = Trim(rest);

  bool has_time = false;
  if (!rest.empty()) {
    std::tm with_time = parsed;
    std::istringstream time_stream(rest);
    time_stream >> std::get_time(&with_time, "%H:%M:%S");
    if (time_stream.fail()) {
      time_stream.clear();
      time_stream.str(rest);
      with_time = parsed;
      time_stream >> std::get_time(&with_time, "%H:%M");
      if (time_stream.fail()) {
        return false;
      }
      with_time.tm_sec = 0;
    }
    parsed = with_time;
    has_time = true;
  }

  // mktime 按本地时区解释，和用户看到的文件时间一致。
  parsed.tm_isdst = -1;
  const std::time_t value = std::mktime(&parsed);
  if (value == static_cast<std::time_t>(-1)) {
    return false;
  }

  *seconds = static_cast<std::int64_t>(value);
  *day_only = !has_time;
  return true;
}

// 解析 ">=", "<=", "=", ">", "<" 前缀；没有前缀时等于「精确匹配」。
bool ParseComparisonPrefix(const std::string &text, CompareOp *op,
                           std::string *rest) {
  struct Prefix {
    const char *text;
    CompareOp op;
  };
  static const Prefix prefixes[] = {{">=", CompareOp::GreaterEqual},
                                    {"<=", CompareOp::LessEqual},
                                    {"=", CompareOp::Equal},
                                    {">", CompareOp::Greater},
                                    {"<", CompareOp::Less}};

  for (const Prefix &prefix : prefixes) {
    const std::string value(prefix.text);
    if (text.compare(0, value.size(), value) == 0) {
      *op = prefix.op;
      *rest = text.substr(value.size());
      return true;
    }
  }

  *op = CompareOp::Equal;
  *rest = text;
  return false;
}

bool ParseSizeTerm(const std::string &body, SearchTerm *term) {
  CompareOp op = CompareOp::Equal;
  std::string rest;
  ParseComparisonPrefix(Trim(body), &op, &rest);

  const std::size_t range = rest.find("..");
  if (range != std::string::npos) {
    std::int64_t lower = 0;
    std::int64_t upper = 0;
    if (!ParseSizeText(rest.substr(0, range), &lower) ||
        !ParseSizeText(rest.substr(range + 2), &upper)) {
      return false;
    }
    if (upper < lower) {
      std::swap(lower, upper);
    }
    term->compare = CompareOp::Equal;
    term->value = lower;
    term->has_upper = true;
    term->upper = upper;
    return true;
  }

  std::int64_t value = 0;
  if (!ParseSizeText(rest, &value)) {
    return false;
  }
  term->compare = op;
  term->value = value;
  return true;
}

bool ParseDateTerm(const std::string &body, SearchTerm *term) {
  CompareOp op = CompareOp::Equal;
  std::string rest;
  ParseComparisonPrefix(Trim(body), &op, &rest);

  const std::size_t range = rest.find("..");
  if (range != std::string::npos) {
    std::int64_t lower = 0;
    std::int64_t upper = 0;
    bool lower_day_only = false;
    bool upper_day_only = false;
    if (!ParseDateText(rest.substr(0, range), &lower, &lower_day_only) ||
        !ParseDateText(rest.substr(range + 2), &upper, &upper_day_only)) {
      return false;
    }
    if (upper_day_only) {
      upper += 86399;
    }
    if (upper < lower) {
      std::swap(lower, upper);
    }
    term->compare = CompareOp::Equal;
    term->value = lower;
    term->has_upper = true;
    term->upper = upper;
    return true;
  }

  std::int64_t value = 0;
  bool day_only = false;
  if (!ParseDateText(rest, &value, &day_only)) {
    return false;
  }
  term->compare = op;
  term->value = value;
  // 只写日期时按「整天」理解：dm:2024-01-01 就是那一天的任意时刻。
  if (day_only && op == CompareOp::Equal) {
    term->has_upper = true;
    term->upper = value + 86399;
  }
  return true;
}

} // namespace

std::string AsciiLower(const std::string &text) {
  std::string output = text;
  for (char &value : output) {
    const auto byte = static_cast<unsigned char>(value);
    if (byte < 0x80U) {
      value = static_cast<char>(std::tolower(byte));
    }
  }
  return output;
}

bool GlobMatch(const std::string &pattern, const std::string &text,
               const bool match_case) {
  std::size_t pattern_index = 0;
  std::size_t text_index = 0;
  std::size_t star_pattern = std::string::npos;
  std::size_t star_text = 0;

  while (text_index < text.size()) {
    if (pattern_index < pattern.size() &&
        (pattern[pattern_index] == '?' ||
         EqualsCharacter(pattern[pattern_index], text[text_index],
                         match_case))) {
      ++pattern_index;
      ++text_index;
    } else if (pattern_index < pattern.size() &&
               pattern[pattern_index] == '*') {
      star_pattern = pattern_index++;
      star_text = text_index;
    } else if (star_pattern != std::string::npos) {
      // 回退到最后一个 *：先让它多吃一个字符再继续比较。
      pattern_index = star_pattern + 1;
      text_index = ++star_text;
    } else {
      return false;
    }
  }

  while (pattern_index < pattern.size() && pattern[pattern_index] == '*') {
    ++pattern_index;
  }
  return pattern_index == pattern.size();
}

std::string BaseName(const std::string &path) {
  if (path.empty()) {
    return {};
  }

  std::size_t end = path.size();
  while (end > 1 && path[end - 1] == '/') {
    --end;
  }

  const std::size_t separator = path.rfind('/', end - 1);
  if (separator == std::string::npos) {
    return path.substr(0, end);
  }
  return path.substr(separator + 1, end - separator - 1);
}

std::string PathExtension(const std::string &path) {
  const std::string name = BaseName(path);
  const std::size_t dot = name.rfind('.');
  // ".bashrc" 没有扩展名；"a." 这种也当作没有扩展名。
  if (dot == std::string::npos || dot == 0 || dot + 1 >= name.size()) {
    return {};
  }
  return name.substr(dot + 1);
}

namespace {

std::int64_t ToUnixSeconds(const std::chrono::system_clock::time_point &value) {
  return std::chrono::duration_cast<std::chrono::seconds>(
             value.time_since_epoch())
      .count();
}

bool MatchesNumber(const SearchTerm &term, const std::int64_t actual) {
  if (term.has_upper) {
    return actual >= term.value && actual <= term.upper;
  }

  switch (term.compare) {
  case CompareOp::Less:
    return actual < term.value;
  case CompareOp::LessEqual:
    return actual <= term.value;
  case CompareOp::Greater:
    return actual > term.value;
  case CompareOp::GreaterEqual:
    return actual >= term.value;
  case CompareOp::Equal:
    return actual == term.value;
  }
  return false;
}

// content: 只读文件开头的一段。整文件读取会让一次搜索变成「把候选文件全读一遍」，
// 与路径索引后端的设计目标冲突；文档里明确说明这是前缀匹配。
constexpr std::size_t ContentProbeBytes = 256U * 1024U;

bool FileContainsText(const std::string &path, const std::string &needle,
                      const bool match_case) {
  if (needle.empty()) {
    return true;
  }

  std::FILE *handle = std::fopen(path.c_str(), "rb");
  if (handle == nullptr) {
    return false;
  }

  std::string haystack;
  haystack.resize(ContentProbeBytes);
  const std::size_t read = std::fread(&haystack[0], 1, haystack.size(), handle);
  std::fclose(handle);
  haystack.resize(read);

  if (!match_case) {
    haystack = AsciiLower(haystack);
  }
  const std::string target = match_case ? needle : AsciiLower(needle);
  return haystack.find(target) != std::string::npos;
}

bool TermNeedsMetadata(const SearchTerm::Kind kind) {
  switch (kind) {
  case SearchTerm::Kind::Size:
  case SearchTerm::Kind::DateCreated:
  case SearchTerm::Kind::DateModified:
  case SearchTerm::Kind::DateAccessed:
  case SearchTerm::Kind::Content:
    return true;
  default:
    return false;
  }
}

// 这里的 metadata 一定有效：需要元数据的词条已在 MatchQuery 里被跳过或补齐。
bool EvaluateTerm(const SearchTerm &term, const std::string &full_path,
                  const std::string &name, const bool match_path,
                  const bool match_case, const bool match_whole_word,
                  const FileMetadata &metadata) {
  switch (term.kind) {
  case SearchTerm::Kind::Substring: {
    const std::string &target = match_path ? full_path : name;
    return ContainsSubstring(target, term.text, match_case, match_whole_word);
  }
  case SearchTerm::Kind::Glob: {
    const std::string &target = match_path ? full_path : name;
    return GlobMatch(term.text, target, match_case);
  }
  case SearchTerm::Kind::Regex: {
    const std::string &target = match_path ? full_path : name;
    if (!term.regex_valid) {
      return ContainsSubstring(target, term.text, match_case, match_whole_word);
    }
    return std::regex_search(target, term.regex_value);
  }
  case SearchTerm::Kind::Extension: {
    const std::string extension = PathExtension(full_path);
    if (extension.empty()) {
      return false;
    }
    const std::string candidate =
        match_case ? extension : AsciiLower(extension);
    for (const std::string &value : term.extensions) {
      const std::string expected = match_case ? value : AsciiLower(value);
      if (candidate == expected) {
        return true;
      }
    }
    return false;
  }
  case SearchTerm::Kind::PathContains:
    return ContainsSubstring(full_path, term.text, match_case,
                             match_whole_word);
  case SearchTerm::Kind::FileNameContains:
    return ContainsSubstring(name, term.text, match_case, match_whole_word);
  case SearchTerm::Kind::Size: {
    // 与 Everything 一致：目录大小按 0 处理（不拿 inode 的目录项大小冒充），
    // 否则 size:>1kb 会把几乎所有目录都算进去，且 !size: 的取反语义会前后矛盾。
    const std::int64_t size = metadata.is_file ? metadata.size : 0;
    return MatchesNumber(term, size);
  }
  case SearchTerm::Kind::DateCreated:
    return metadata.has_created &&
           MatchesNumber(term, ToUnixSeconds(metadata.created));
  case SearchTerm::Kind::DateModified:
    return metadata.has_modified &&
           MatchesNumber(term, ToUnixSeconds(metadata.modified));
  case SearchTerm::Kind::DateAccessed:
    return metadata.has_accessed &&
           MatchesNumber(term, ToUnixSeconds(metadata.accessed));
  case SearchTerm::Kind::Content:
    return metadata.is_file &&
           FileContainsText(full_path, term.text, match_case);
  }

  return false;
}

// metadata 为空表示「只有路径可用」的粗筛阶段：大小/时间/内容词条整体跳过，
// 否则每次发现新路径都要 stat 一次，locate/fd 的速度优势全没了。
// 注意必须在取反之前跳过——把它们当 true 再取反会变成 false，候选会被全部误杀。
// metadata 为空表示「只有路径可用」的粗筛阶段：大小/时间/内容词条整体跳过，
// 否则每次发现新路径都要 stat 一次，locate/fd 的速度优势全没了。
// 注意必须在取反之前跳过——把它们当 true 再取反会变成 false，候选会被全部误杀。
bool MatchQuery(const SearchQuery &query, const std::string &full_path,
                const FileMetadata *metadata) {
  if (query.terms.empty()) {
    return true;
  }

  const std::string name = BaseName(full_path);
  for (const SearchTerm &term : query.terms) {
    if (metadata == nullptr && TermNeedsMetadata(term.kind)) {
      continue;
    }

    bool matched =
        EvaluateTerm(term, full_path, name, query.match_path, query.match_case,
                     query.match_whole_word, *metadata);
    if (term.negated) {
      matched = !matched;
    }
    if (!matched) {
      return false;
    }
  }

  return true;
}

} // namespace

bool SearchQuery::matches(const std::string &full_path) const {
  return MatchQuery(*this, full_path, nullptr);
}

bool SearchQuery::matches(const std::string &full_path,
                          const FileMetadata &metadata) const {
  return MatchQuery(*this, full_path, &metadata);
}

bool SearchQuery::needs_metadata() const noexcept {
  for (const SearchTerm &term : terms) {
    if (TermNeedsMetadata(term.kind)) {
      return true;
    }
  }
  return false;
}

SearchQuery ParseSearchQuery(const std::string &search_text,
                             const bool match_path, const bool match_case,
                             const bool match_whole_word,
                             const bool use_regex) {
  SearchQuery query;
  query.match_path = match_path;
  query.match_case = match_case;
  query.match_whole_word = match_whole_word;
  query.use_regex = use_regex;

  for (const std::string &token : Tokenize(search_text)) {
    if (token.empty()) {
      continue;
    }

    SearchTerm term;
    std::string body = token;
    if (body.size() > 1 && body.front() == '!') {
      term.negated = true;
      body.erase(body.begin());
    }

    if (body.empty()) {
      continue;
    }

    if (StartsWithIgnoreCase(body, "ext:")) {
      term.kind = SearchTerm::Kind::Extension;
      term.extensions = SplitExtensions(body.substr(4));
      if (term.extensions.empty()) {
        continue;
      }
      query.terms.push_back(term);
      continue;
    }

    if (StartsWithIgnoreCase(body, "size:")) {
      if (ParseSizeTerm(body.substr(5), &term)) {
        term.kind = SearchTerm::Kind::Size;
        query.terms.push_back(term);
        continue;
      }
      // 解析失败就退化成普通文本（例如用户就是想找名字里含 "size:x" 的文件）。
    }

    if (StartsWithIgnoreCase(body, "dc:")) {
      if (ParseDateTerm(body.substr(3), &term)) {
        term.kind = SearchTerm::Kind::DateCreated;
        query.terms.push_back(term);
        continue;
      }
    }
    if (StartsWithIgnoreCase(body, "datecreated:")) {
      if (ParseDateTerm(body.substr(12), &term)) {
        term.kind = SearchTerm::Kind::DateCreated;
        query.terms.push_back(term);
        continue;
      }
    }
    if (StartsWithIgnoreCase(body, "dm:")) {
      if (ParseDateTerm(body.substr(3), &term)) {
        term.kind = SearchTerm::Kind::DateModified;
        query.terms.push_back(term);
        continue;
      }
    }
    if (StartsWithIgnoreCase(body, "datemodified:")) {
      if (ParseDateTerm(body.substr(13), &term)) {
        term.kind = SearchTerm::Kind::DateModified;
        query.terms.push_back(term);
        continue;
      }
    }
    if (StartsWithIgnoreCase(body, "da:")) {
      if (ParseDateTerm(body.substr(3), &term)) {
        term.kind = SearchTerm::Kind::DateAccessed;
        query.terms.push_back(term);
        continue;
      }
    }
    if (StartsWithIgnoreCase(body, "dateaccessed:")) {
      if (ParseDateTerm(body.substr(13), &term)) {
        term.kind = SearchTerm::Kind::DateAccessed;
        query.terms.push_back(term);
        continue;
      }
    }

    if (StartsWithIgnoreCase(body, "content:")) {
      term.kind = SearchTerm::Kind::Content;
      term.text = Trim(body.substr(8));
      if (!term.text.empty()) {
        query.terms.push_back(term);
      }
      continue;
    }

    if (StartsWithIgnoreCase(body, "path:")) {
      term.kind = SearchTerm::Kind::PathContains;
      term.text = Trim(body.substr(5));
      if (!term.text.empty()) {
        query.terms.push_back(term);
      }
      continue;
    }

    if (StartsWithIgnoreCase(body, "file:")) {
      term.kind = SearchTerm::Kind::FileNameContains;
      term.text = Trim(body.substr(5));
      if (!term.text.empty()) {
        query.terms.push_back(term);
      }
      continue;
    }

    term.text = body;
    if (use_regex) {
      term.kind = SearchTerm::Kind::Regex;
      try {
        term.regex_value = std::regex(
            body, match_case ? std::regex::ECMAScript
                             : std::regex::ECMAScript | std::regex::icase);
        term.regex_valid = true;
      } catch (const std::regex_error &) {
        // 非法正则退化成普通文本，至少让用户拿到一些结果而不是空列表。
        term.regex_valid = false;
      }
    } else {
      // Everything 默认支持通配符；含 * 或 ? 的 token 按整串 glob 匹配。
      term.kind = body.find('*') != std::string::npos ||
                          body.find('?') != std::string::npos
                      ? SearchTerm::Kind::Glob
                      : SearchTerm::Kind::Substring;
    }

    query.terms.push_back(term);
  }

  return query;
}
} // namespace Posix
} // namespace Everything
} // namespace Lib
