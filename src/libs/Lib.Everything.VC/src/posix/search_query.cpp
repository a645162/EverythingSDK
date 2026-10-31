/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_query.cpp
 * Brief: Everything 搜索语法子集的解析与匹配实现。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#include "search_query.h"

#include <cctype>
#include <cstddef>

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

bool SearchQuery::matches(const std::string &full_path) const {
  if (terms.empty()) {
    return true;
  }

  const std::string name = BaseName(full_path);
  for (const SearchTerm &term : terms) {
    bool matched = false;
    switch (term.kind) {
    case SearchTerm::Kind::Substring: {
      const std::string &target = match_path ? full_path : name;
      matched =
          ContainsSubstring(target, term.text, match_case, match_whole_word);
      break;
    }
    case SearchTerm::Kind::Glob: {
      const std::string &target = match_path ? full_path : name;
      matched = GlobMatch(term.text, target, match_case);
      break;
    }
    case SearchTerm::Kind::Regex: {
      if (!term.regex_valid) {
        const std::string &target = match_path ? full_path : name;
        matched =
            ContainsSubstring(target, term.text, match_case, match_whole_word);
      } else {
        const std::string &target = match_path ? full_path : name;
        matched = std::regex_search(target, term.regex_value);
      }
      break;
    }
    case SearchTerm::Kind::Extension: {
      const std::string extension = PathExtension(full_path);
      if (!extension.empty()) {
        const std::string candidate =
            match_case ? extension : AsciiLower(extension);
        for (const std::string &value : term.extensions) {
          const std::string expected = match_case ? value : AsciiLower(value);
          if (candidate == expected) {
            matched = true;
            break;
          }
        }
      }
      break;
    }
    case SearchTerm::Kind::PathContains:
      matched =
          ContainsSubstring(full_path, term.text, match_case, match_whole_word);
      break;
    case SearchTerm::Kind::FileNameContains:
      matched =
          ContainsSubstring(name, term.text, match_case, match_whole_word);
      break;
    }

    if (term.negated) {
      matched = !matched;
    }

    if (!matched) {
      return false;
    }
  }

  return true;
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
