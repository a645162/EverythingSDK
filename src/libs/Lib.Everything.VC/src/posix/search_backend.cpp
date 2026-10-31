/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_backend.cpp
 * Brief: POSIX 搜索后端探测与候选路径枚举实现。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#include "search_backend.h"

#include "process_runner.h"

#include <algorithm>
#include <atomic>
#include <cstdlib>
#include <cstring>

#include <dirent.h>
#include <sys/stat.h>
#include <unistd.h>

namespace {
constexpr std::size_t LocateDefaultLimit = 200000;

std::string EnvironmentValue(const char *name) {
  const char *value = std::getenv(name);
  return value != nullptr ? std::string(value) : std::string{};
}

bool StartsWith(const std::string &text, const std::string &prefix) {
  return text.size() >= prefix.size() &&
         text.compare(0, prefix.size(), prefix) == 0;
}

// plocate/locate 使用 shell glob（fnmatch）。用户关键字里的元字符必须
// 折成字符类，否则 "a*b" 会变成通配从而召回大量无关结果。
std::string EscapeGlob(const std::string &text) {
  std::string output;
  output.reserve(text.size() * 2);
  for (const char value : text) {
    switch (value) {
    case '*':
      output += "[*]";
      break;
    case '?':
      output += "[?]";
      break;
    case '[':
      output += "[[]";
      break;
    case ']':
      output += "[]]";
      break;
    case '\\':
      output += "[\\]";
      break;
    default:
      output.push_back(value);
      break;
    }
  }
  return output;
}

// glob 形式只需要转义字符类相关字符，保留 * 与 ? 的通配语义。
std::string EscapeGlobKeepingWildcards(const std::string &text) {
  std::string output;
  output.reserve(text.size() * 2);
  for (const char value : text) {
    switch (value) {
    case '[':
      output += "[[]";
      break;
    case ']':
      output += "[]]";
      break;
    case '\\':
      output += "[\\]";
      break;
    default:
      output.push_back(value);
      break;
    }
  }
  return output;
}

std::vector<std::string> LocateDatabaseCandidates() {
  std::vector<std::string> candidates;
  const std::string configured = EnvironmentValue("EVERYTHING_SDK_LOCATE_DB");
  if (!configured.empty()) {
    candidates.push_back(configured);
  }

  candidates.push_back("/var/lib/plocate/plocate.db");
  candidates.push_back("/var/lib/mlocate/mlocate.db");
  candidates.push_back("/var/lib/locatedb");
  candidates.push_back("/var/cache/locate/locatedb");
  candidates.push_back("/usr/var/locatedb");
  return candidates;
}

std::string FirstExistingDatabase() {
  for (const std::string &candidate : LocateDatabaseCandidates()) {
    if (Lib::Everything::Posix::FileExists(candidate)) {
      return candidate;
    }
  }
  return {};
}

std::vector<std::string> RootsFromEnvironment() {
  std::vector<std::string> roots;
  const std::string configured =
      EnvironmentValue("EVERYTHING_SDK_SEARCH_ROOTS");
  if (configured.empty()) {
    return roots;
  }

  std::size_t begin = 0;
  while (begin <= configured.size()) {
    const std::size_t end = configured.find(':', begin);
    std::string root = configured.substr(
        begin, end == std::string::npos ? std::string::npos : end - begin);
    if (root.size() > 1 && root.back() == '/') {
      root.pop_back();
    }
    if (!root.empty()) {
      roots.push_back(root);
    }
    if (end == std::string::npos) {
      break;
    }
    begin = end + 1;
  }
  return roots;
}

bool MatchesRoots(const std::string &path,
                  const std::vector<std::string> &roots) {
  if (roots.empty()) {
    return true;
  }

  for (const std::string &root : roots) {
    if (root == "/" || path == root ||
        (StartsWith(path, root) && path.size() > root.size() &&
         path[root.size()] == '/')) {
      return true;
    }
  }
  return false;
}

// 把可表达的“正向”条件转成 glob（路径包含/文件名包含/普通文本）。
// 正则与排除项无法用 glob 表达，留给进程内精确过滤。
std::vector<std::string>
LocatePatterns(const Lib::Everything::Posix::SearchQuery &query) {
  std::vector<std::string> patterns;
  for (const Lib::Everything::Posix::SearchTerm &term : query.terms) {
    if (term.negated) {
      continue;
    }

    switch (term.kind) {
    case Lib::Everything::Posix::SearchTerm::Kind::Substring:
    case Lib::Everything::Posix::SearchTerm::Kind::PathContains:
    case Lib::Everything::Posix::SearchTerm::Kind::FileNameContains:
      patterns.push_back("*" + EscapeGlob(term.text) + "*");
      break;
    case Lib::Everything::Posix::SearchTerm::Kind::Glob:
      patterns.push_back(EscapeGlobKeepingWildcards(term.text));
      break;
    default:
      break;
    }
  }
  return patterns;
}

std::vector<std::string>
ExtensionGlobs(const Lib::Everything::Posix::SearchQuery &query) {
  std::vector<std::string> globs;
  for (const Lib::Everything::Posix::SearchTerm &term : query.terms) {
    if (term.negated ||
        term.kind != Lib::Everything::Posix::SearchTerm::Kind::Extension) {
      continue;
    }

    for (const std::string &extension : term.extensions) {
      globs.push_back("*." + EscapeGlob(extension));
    }
  }
  return globs;
}

void AppendLocateRecord(std::vector<std::string> &paths,
                        const std::string &record,
                        const std::vector<std::string> &roots,
                        const std::size_t cap, bool &truncated) {
  if (paths.size() >= cap) {
    truncated = true;
    return;
  }
  if (record.empty() || record.front() != '/') {
    return;
  }
  if (!MatchesRoots(record, roots)) {
    return;
  }
  paths.push_back(record);
}

Lib::Everything::Posix::CandidateCollection
CollectFromLocate(const Lib::Everything::Posix::BackendInfo &backend,
                  const Lib::Everything::Posix::SearchQuery &query,
                  const std::size_t cap) {
  Lib::Everything::Posix::CandidateCollection collection;
  const std::vector<std::string> roots = RootsFromEnvironment();
  const std::vector<std::string> patterns = LocatePatterns(query);
  const std::vector<std::string> extension_globs = ExtensionGlobs(query);

  const auto consumer = [&](const std::string &record) {
    AppendLocateRecord(collection.paths, record, roots, cap,
                       collection.truncated);
    return collection.paths.size() < cap;
  };

  const auto run_once = [&](const std::vector<std::string> &extra_patterns) {
    std::vector<std::string> arguments;
    arguments.push_back(backend.executable);
    arguments.push_back("-i");
    arguments.push_back("-0");
    arguments.push_back("-l");
    arguments.push_back(std::to_string(cap));
    for (const std::string &pattern : patterns) {
      arguments.push_back(pattern);
    }
    for (const std::string &pattern : extra_patterns) {
      arguments.push_back(pattern);
    }
    return Lib::Everything::Posix::RunProcess(arguments, '\0', 0, consumer);
  };

  if (!patterns.empty() || extension_globs.empty()) {
    // 已有路径条件，或根本没有可表达的扩展名：跑一次即可。
    const Lib::Everything::Posix::ProcessOutcome outcome = run_once({});
    if (!outcome.started) {
      collection.error = outcome.error;
    }
    return collection;
  }

  // 只剩扩展名条件：plocate 的多 pattern 是 AND，而扩展名列表是 OR，
  // 因此按扩展名逐个查询再合并（每次都是同一份内存数据库，代价很小）。
  for (const std::string &glob : extension_globs) {
    if (collection.paths.size() >= cap) {
      collection.truncated = true;
      break;
    }
    const Lib::Everything::Posix::ProcessOutcome outcome = run_once({glob});
    if (!outcome.started) {
      collection.error = outcome.error;
      break;
    }
  }
  return collection;
}

// fd 的 -a 会给目录补上结尾斜杠，统一去掉，保证与 locate/find 的路径形态一致。
std::string TrimTrailingSlash(const std::string &path) {
  std::size_t end = path.size();
  while (end > 1 && path[end - 1] == '/') {
    --end;
  }
  return path.substr(0, end);
}

// 只把「一定能覆盖所有可能命中项」的条件交给 fd 做粗筛，其余交给进程内匹配。
// 例如 MatchPath 打开时的文本条件若当成文件名模式，会漏掉只出现在目录里的匹配。
void AppendFdPatternArguments(const Lib::Everything::Posix::SearchQuery &query,
                              std::vector<std::string> &arguments) {
  using Lib::Everything::Posix::SearchTerm;

  for (const SearchTerm &term : query.terms) {
    if (term.negated) {
      continue;
    }

    switch (term.kind) {
    case SearchTerm::Kind::FileNameContains:
      arguments.push_back("-F");
      arguments.push_back(term.text);
      return;
    case SearchTerm::Kind::Substring:
      if (!query.match_path) {
        arguments.push_back("-F");
        arguments.push_back(term.text);
        return;
      }
      break;
    case SearchTerm::Kind::Glob:
      if (!query.match_path) {
        arguments.push_back("-g");
        arguments.push_back(EscapeGlobKeepingWildcards(term.text));
        return;
      }
      break;
    default:
      break;
    }
  }

  // 没有可用作粗筛的条件：fd 的 "." 匹配所有条目，由进程内匹配做最终判定。
  arguments.push_back(".");
}

Lib::Everything::Posix::CandidateCollection
CollectFromFd(const Lib::Everything::Posix::BackendInfo &backend,
              const Lib::Everything::Posix::SearchQuery &query,
              const std::size_t cap) {
  Lib::Everything::Posix::CandidateCollection collection;
  const std::vector<std::string> roots =
      Lib::Everything::Posix::DefaultSearchRoots();

  std::vector<std::string> arguments;
  arguments.push_back(backend.executable);
  arguments.push_back("-i"); // 与 Everything 默认一致：大小写不敏感
  arguments.push_back("-0"); // NUL 分隔，文件名里可以有换行
  arguments.push_back("-a"); // 输出绝对路径
  arguments.push_back("-H"); // 包含隐藏项
  arguments.push_back("-I"); // 不受 .gitignore/.ignore 影响

  // 扩展名列表在 Everything 里是 OR，fd 的多个 -e 同样是 OR。
  for (const Lib::Everything::Posix::SearchTerm &term : query.terms) {
    if (term.negated ||
        term.kind != Lib::Everything::Posix::SearchTerm::Kind::Extension) {
      continue;
    }
    for (const std::string &extension : term.extensions) {
      arguments.push_back("-e");
      arguments.push_back(extension);
    }
  }

  // 根目录为 / 时跳过伪文件系统：fd 的 --exclude 按相对路径/名字匹配，
  // 这些名字在真实数据目录里极少出现，误伤面很小。
  for (const std::string &root : roots) {
    if (root != "/") {
      continue;
    }
    const char *special[] = {"proc", "sys", "dev", "run", "snap"};
    for (std::size_t index = 0; index < sizeof(special) / sizeof(special[0]);
         ++index) {
      arguments.push_back("--exclude");
      arguments.push_back(special[index]);
    }
    break;
  }

  AppendFdPatternArguments(query, arguments);
  for (const std::string &root : roots) {
    arguments.push_back(root);
  }

  const auto consumer = [&](const std::string &record) {
    if (collection.paths.size() >= cap) {
      collection.truncated = true;
      return false;
    }

    const std::string path = TrimTrailingSlash(record);
    if (!path.empty()) {
      collection.paths.push_back(path);
    }
    return true;
  };

  const Lib::Everything::Posix::ProcessOutcome outcome =
      Lib::Everything::Posix::RunProcess(arguments, '\0', 0, consumer);
  if (!outcome.started) {
    collection.error = outcome.error;
  }
  return collection;
}

Lib::Everything::Posix::CandidateCollection
CollectFromFind(const Lib::Everything::Posix::SearchQuery &query,
                const std::size_t cap) {
  Lib::Everything::Posix::CandidateCollection collection;
  const std::vector<std::string> roots =
      Lib::Everything::Posix::DefaultSearchRoots();

  std::vector<std::string> arguments;
  arguments.push_back(Lib::Everything::Posix::FindExecutable("find"));
  for (const std::string &root : roots) {
    arguments.push_back(root);
  }

  bool prune_special = false;
  for (const std::string &root : roots) {
    if (root == "/") {
      prune_special = true;
      break;
    }
  }

  if (prune_special) {
    arguments.push_back("(");
    const char *special[] = {"/proc", "/sys", "/dev", "/run", "/snap"};
    for (std::size_t index = 0; index < sizeof(special) / sizeof(special[0]);
         ++index) {
      if (index > 0) {
        arguments.push_back("-o");
      }
      arguments.push_back("-path");
      arguments.push_back(special[index]);
    }
    arguments.push_back(")");
    arguments.push_back("-prune");
    arguments.push_back("-o");
  }

  const std::vector<std::string> extensions = ExtensionGlobs(query);
  if (!extensions.empty()) {
    arguments.push_back("(");
    for (std::size_t index = 0; index < extensions.size(); ++index) {
      if (index > 0) {
        arguments.push_back("-o");
      }
      arguments.push_back("-ipath");
      arguments.push_back(extensions[index]);
    }
    arguments.push_back(")");
  }

  const std::vector<std::string> patterns = LocatePatterns(query);
  for (const std::string &pattern : patterns) {
    arguments.push_back("-ipath");
    arguments.push_back(pattern);
  }
  arguments.push_back("-print0");

  const auto consumer = [&](const std::string &record) {
    if (collection.paths.size() >= cap) {
      collection.truncated = true;
      return false;
    }
    if (!record.empty()) {
      collection.paths.push_back(record);
    }
    return true;
  };

  const Lib::Everything::Posix::ProcessOutcome outcome =
      Lib::Everything::Posix::RunProcess(arguments, '\0', 0, consumer);
  if (!outcome.started) {
    collection.error = outcome.error;
  }
  return collection;
}

void ScanDirectory(const std::string &directory,
                   const Lib::Everything::Posix::SearchQuery &query,
                   const std::size_t cap,
                   Lib::Everything::Posix::CandidateCollection &collection,
                   const int depth) {
  if (depth > 64 || collection.paths.size() >= cap) {
    collection.truncated = collection.paths.size() >= cap;
    return;
  }

  DIR *handle = ::opendir(directory.c_str());
  if (handle == nullptr) {
    return;
  }

  while (collection.paths.size() < cap) {
    struct dirent *entry = ::readdir(handle);
    if (entry == nullptr) {
      break;
    }

    const std::string name(entry->d_name);
    if (name == "." || name == "..") {
      continue;
    }

    const std::string path =
        directory == "/" ? directory + name : directory + "/" + name;

    if (query.matches(path)) {
      collection.paths.push_back(path);
    }

    bool is_directory = false;
    if (entry->d_type == DT_DIR) {
      is_directory = true;
    } else if (entry->d_type == DT_UNKNOWN) {
      struct stat status{};
      is_directory =
          ::lstat(path.c_str(), &status) == 0 && S_ISDIR(status.st_mode) != 0;
    }

    // 不跟随符号链接，避免目录环把扫描卡死。
    if (is_directory) {
      ScanDirectory(path, query, cap, collection, depth + 1);
    }
  }

  ::closedir(handle);
}

Lib::Everything::Posix::CandidateCollection
CollectFromScan(const Lib::Everything::Posix::SearchQuery &query,
                const std::size_t cap) {
  Lib::Everything::Posix::CandidateCollection collection;
  const std::vector<std::string> roots =
      Lib::Everything::Posix::DefaultSearchRoots();
  for (const std::string &root : roots) {
    ScanDirectory(root, query, cap, collection, 0);
    if (collection.paths.size() >= cap) {
      collection.truncated = true;
      break;
    }
  }
  return collection;
}

// 手动指定后端的存放位置：-1 表示自动。搜索跑在调用方的工作线程上，
// 因此用原子变量而不是 std::string，避免读写竞争。
std::atomic<int> &PreferenceIndex() {
  static std::atomic<int> index{-1};
  return index;
}

// 构造指定后端的探测结果。executable 为空表示该后端在本机不可用；
// Scan 不依赖外部可执行文件，因此永远可用。
Lib::Everything::Posix::BackendInfo
ResolveBackendKind(const Lib::Everything::Posix::BackendKind kind) {
  using Lib::Everything::Posix::BackendInfo;
  using Lib::Everything::Posix::BackendKind;

  BackendInfo info;
  info.kind = kind;

  switch (kind) {
  case BackendKind::Locate: {
    const std::string database = FirstExistingDatabase();
    if (database.empty()) {
      info.reason = "locate database missing";
      return info;
    }

    const std::string plocate =
        Lib::Everything::Posix::FindExecutable("plocate");
    if (!plocate.empty()) {
      info.executable = plocate;
      info.database_loaded = true;
      info.reason = "plocate database " + database;
      return info;
    }

    const std::string locate = Lib::Everything::Posix::FindExecutable("locate");
    if (!locate.empty()) {
      info.executable = locate;
      info.database_loaded = true;
      info.reason = "locate database " + database;
      return info;
    }

    info.reason = "locate executable missing";
    return info;
  }
  case BackendKind::Fd: {
    // Debian/Ubuntu 的包名叫 fd-find，可执行文件是 fdfind，两个名字都要探测。
    const std::string fd = Lib::Everything::Posix::FindExecutable("fd");
    const std::string fdfind = Lib::Everything::Posix::FindExecutable("fdfind");
    info.executable = fd.empty() ? fdfind : fd;
    info.reason = info.executable.empty() ? "fd executable missing"
                                          : "fd executable " + info.executable;
    return info;
  }
  case BackendKind::Find: {
    info.executable = Lib::Everything::Posix::FindExecutable("find");
    info.reason = info.executable.empty()
                      ? "find executable missing"
                      : "find executable " + info.executable;
    return info;
  }
  case BackendKind::Scan:
    info.reason = "in-process scan";
    return info;
  }

  return info;
}

bool IsAvailable(const Lib::Everything::Posix::BackendInfo &info) {
  using Lib::Everything::Posix::BackendKind;
  if (info.kind == BackendKind::Scan) {
    return true;
  }
  return !info.executable.empty();
}

// 显式指定某个后端后，不可用时要按什么顺序回退。
// 指定 fd/find 时跳过 locate：索引可能过期，用户点名了就别用旧快照。
std::vector<Lib::Everything::Posix::BackendKind>
FallbackOrder(const Lib::Everything::Posix::BackendKind forced) {
  using Lib::Everything::Posix::BackendKind;
  switch (forced) {
  case BackendKind::Locate:
    return {BackendKind::Locate, BackendKind::Fd, BackendKind::Find,
            BackendKind::Scan};
  case BackendKind::Fd:
    return {BackendKind::Fd, BackendKind::Find, BackendKind::Scan};
  case BackendKind::Find:
    return {BackendKind::Find, BackendKind::Scan};
  case BackendKind::Scan:
    return {BackendKind::Scan};
  }
  return {BackendKind::Locate, BackendKind::Fd, BackendKind::Find,
          BackendKind::Scan};
}

Lib::Everything::Posix::BackendInfo FirstAvailableFrom(
    const std::vector<Lib::Everything::Posix::BackendKind> &order) {
  for (const Lib::Everything::Posix::BackendKind candidate : order) {
    const Lib::Everything::Posix::BackendInfo info =
        ResolveBackendKind(candidate);
    if (IsAvailable(info)) {
      return info;
    }
  }

  // 理论上到不了这里：FallbackOrder 的最后一项一定是 Scan。
  return ResolveBackendKind(Lib::Everything::Posix::BackendKind::Scan);
}
} // namespace

namespace Lib {
namespace Everything {
namespace Posix {
std::string BackendName(const BackendKind kind) {
  switch (kind) {
  case BackendKind::Locate:
    return "locate";
  case BackendKind::Fd:
    return "fd";
  case BackendKind::Find:
    return "find";
  case BackendKind::Scan:
    return "scan";
  }
  return "scan";
}

BackendInfo DetectBackend() {
  // 1) 显式 API 偏好优先，便于界面让用户手动指定后端。
  const int preferred = PreferenceIndex().load(std::memory_order_relaxed);
  if (preferred >= 0) {
    BackendInfo forced =
        FirstAvailableFrom(FallbackOrder(static_cast<BackendKind>(preferred)));
    forced.reason += " (preferred)";
    return forced;
  }

  // 2) 环境变量，保留无条件脚本化能力。
  BackendKind forced_kind{};
  if (TryParseBackendName(EnvironmentValue("EVERYTHING_SDK_SEARCH_BACKEND"),
                          &forced_kind)) {
    BackendInfo forced = FirstAvailableFrom(FallbackOrder(forced_kind));
    forced.reason += " (EVERYTHING_SDK_SEARCH_BACKEND)";
    return forced;
  }

  // 3) 自动：locate → fd → find → scan。
  return FirstAvailableFrom(FallbackOrder(BackendKind::Locate));
}

bool TryParseBackendName(const std::string &name, BackendKind *kind) {
  BackendKind parsed{};
  if (name == "locate") {
    parsed = BackendKind::Locate;
  } else if (name == "fd") {
    parsed = BackendKind::Fd;
  } else if (name == "find") {
    parsed = BackendKind::Find;
  } else if (name == "scan") {
    parsed = BackendKind::Scan;
  } else {
    return false;
  }

  if (kind != nullptr) {
    *kind = parsed;
  }
  return true;
}

std::vector<BackendKind> AvailableBackends() {
  std::vector<BackendKind> backends;
  const BackendKind candidates[] = {BackendKind::Locate, BackendKind::Fd,
                                    BackendKind::Find, BackendKind::Scan};
  for (const BackendKind candidate : candidates) {
    if (IsAvailable(ResolveBackendKind(candidate))) {
      backends.push_back(candidate);
    }
  }
  return backends;
}

void SetBackendPreference(const std::string &name) {
  BackendKind kind{};
  if (name.empty()) {
    PreferenceIndex().store(-1, std::memory_order_relaxed);
    return;
  }

  // 无法识别的名字不写入，避免把用户的输入变成「永远自动」而没有任何反馈；
  // 调用方可用 BackendPreference() 确认是否真的生效。
  if (!TryParseBackendName(name, &kind)) {
    return;
  }
  PreferenceIndex().store(static_cast<int>(kind), std::memory_order_relaxed);
}

std::string BackendPreference() {
  const int preferred = PreferenceIndex().load(std::memory_order_relaxed);
  if (preferred < 0) {
    return {};
  }
  return BackendName(static_cast<BackendKind>(preferred));
}

std::vector<std::string> DefaultSearchRoots() {
  std::vector<std::string> roots = RootsFromEnvironment();
  if (!roots.empty()) {
    return roots;
  }

  const std::string home = EnvironmentValue("HOME");
  roots.push_back(home.empty() ? "/" : home);
  return roots;
}

CandidateCollection CollectCandidates(const SearchQuery &query,
                                      const std::size_t cap) {
  const BackendInfo backend = DetectBackend();
  CandidateCollection collection;
  switch (backend.kind) {
  case BackendKind::Locate:
    collection = CollectFromLocate(backend, query, cap);
    break;
  case BackendKind::Fd:
    collection = CollectFromFd(backend, query, cap);
    break;
  case BackendKind::Find:
    collection = CollectFromFind(query, cap);
    break;
  case BackendKind::Scan:
    collection = CollectFromScan(query, cap);
    break;
  }

  // locate 按扩展名分多次查询、find 在重叠根目录下都会产生重复项。
  std::sort(collection.paths.begin(), collection.paths.end());
  collection.paths.erase(
      std::unique(collection.paths.begin(), collection.paths.end()),
      collection.paths.end());
  return collection;
}
} // namespace Posix
} // namespace Everything
} // namespace Lib
