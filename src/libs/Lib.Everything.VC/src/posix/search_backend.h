/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/search_backend.h
 * Brief: POSIX 搜索后端探测与候选路径枚举（plocate/locate → fd → find → 进程内扫描）。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include "search_query.h"

#include <cstddef>
#include <string>
#include <vector>

namespace Lib
{
namespace Everything
{
namespace Posix
{
enum class BackendKind
{
    Locate,
    Fd,
    Find,
    Scan
};

struct BackendInfo
{
    BackendKind kind{BackendKind::Scan};
    std::string executable;
    bool database_loaded{false};
    std::string reason;
};

struct CandidateCollection
{
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

std::vector<std::string> DefaultSearchRoots();

// cap 是候选路径的硬上限，用来防止“无索引时扫 /”把内存吃光。
CandidateCollection CollectCandidates(const SearchQuery& query, std::size_t cap);
} // namespace Posix
} // namespace Everything
} // namespace Lib
