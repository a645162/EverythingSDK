/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/file_metadata.h
 * Brief: POSIX 文件元数据读取（大小、时间、类型、权限位）。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include <chrono>
#include <cstdint>
#include <string>

namespace Lib
{
namespace Everything
{
namespace Posix
{
struct FileMetadata
{
    bool exists{false};
    bool is_file{false};
    bool is_folder{false};
    bool is_symlink{false};
    std::int64_t size{0};
    std::uint32_t attributes{0};

    bool has_created{false};
    std::chrono::system_clock::time_point created;

    bool has_modified{false};
    std::chrono::system_clock::time_point modified;

    bool has_accessed{false};
    std::chrono::system_clock::time_point accessed;
};

// 使用 statx 获取 birth time（Linux）；内核/文件系统不支持时回退到
// ctime，并在文档中说明这是“创建时间”的近似值。
FileMetadata ReadFileMetadata(const std::string& path);
} // namespace Posix
} // namespace Everything
} // namespace Lib
