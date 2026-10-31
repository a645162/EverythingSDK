/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_backend.h
 * Brief: POSIX 搜索后端探测与候选路径枚举（plocate/locate → fd → find → 进程内扫描）。
 * Author: Haomin Kong
 * Email: a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include "search_query.h"

#include <cstddef>
#include <string>
#include <vector>

namespace Lib {
namespace Everything {
namespace Posix {
enum class BackendKind { Locate, Fd, Find, Scan };

struct BackendInfo {
  BackendKind kind{BackendKind::Scan};
  std::string executable;
  bool database_loaded{false};
  std::string reason;
};

struct CandidateCollection {
  std::vector<std::string> paths;
  bool truncated{false};
  std::string error;
};

// 探测顺序：plocate → locate（且数据库存在）→ fd → find → 进程内扫描。
// 可用环境变量强制指定：
//   EVERYTHING_SDK_SEARCH_BACKEND=locate|fd|find|scan
//   EVERYTHING_SDK_LOCATE_DB=/path/to/plocate.db
//   EVERYTHING_SDK_SEARCH_ROOTS=/a:/b
BackendInfo DetectBackend();
std::string BackendName(BackendKind kind);

// 解析后端名称；无法识别时返回 false（不会把未知值当成 scan）。
bool TryParseBackendName(const std::string &name, BackendKind *kind);

// 本机实际可用的后端，按优先级顺序排列；scan 永远可用，因此结果至少有一项。
std::vector<BackendKind> AvailableBackends();

// 手动指定的后端偏好。空字符串表示「自动」，恢复内置优先级。
// 优先级：显式 API 偏好 > EVERYTHING_SDK_SEARCH_BACKEND > 自动探测。
// 指定了但本机不可用时会按 FallbackOrder 继续回退，不会直接失败。
// 这里用原子变量保存，因为搜索跑在调用方的工作线程上。
void SetBackendPreference(const std::string &name);
std::string BackendPreference();

std::vector<std::string> DefaultSearchRoots();

// cap 是候选路径的硬上限，用来防止“无索引时扫 /”把内存吃光。
CandidateCollection CollectCandidates(const SearchQuery &query,
                                      std::size_t cap);
} // namespace Posix
} // namespace Everything
} // namespace Lib
