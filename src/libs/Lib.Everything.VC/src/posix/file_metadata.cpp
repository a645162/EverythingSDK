/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/file_metadata.cpp
 * Brief: POSIX 文件元数据读取实现。
 * Author: Haomin Kong
 * Email: a645162@163.com
 * Created: 2026-10-31
 */

// statx 与 AT_FDCWD 需要 _GNU_SOURCE，必须出现在任何系统头文件之前。
#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif

#include "file_metadata.h"

#include <ctime>

#include <fcntl.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

namespace {
using Lib::Everything::Posix::FileMetadata;

std::chrono::system_clock::time_point ToTimePoint(const std::time_t seconds,
                                                  const long nanoseconds) {
  return std::chrono::system_clock::time_point(
      std::chrono::duration_cast<std::chrono::system_clock::duration>(
          std::chrono::seconds(seconds) +
          std::chrono::nanoseconds(nanoseconds)));
}

void FillCommon(FileMetadata &metadata, const std::int64_t size,
                const std::uint32_t mode, const std::time_t created_seconds,
                const long created_nanoseconds, const bool has_created,
                const std::time_t modified_seconds,
                const long modified_nanoseconds,
                const std::time_t accessed_seconds,
                const long accessed_nanoseconds) {
  metadata.exists = true;
  metadata.size = size;
  metadata.attributes = mode;
  metadata.is_file = S_ISREG(mode) != 0;
  metadata.is_folder = S_ISDIR(mode) != 0;
  metadata.is_symlink = S_ISLNK(mode) != 0;

  if (has_created) {
    metadata.has_created = true;
    metadata.created = ToTimePoint(created_seconds, created_nanoseconds);
  }

  metadata.has_modified = true;
  metadata.modified = ToTimePoint(modified_seconds, modified_nanoseconds);
  metadata.has_accessed = true;
  metadata.accessed = ToTimePoint(accessed_seconds, accessed_nanoseconds);
}
} // namespace

namespace Lib {
namespace Everything {
namespace Posix {
FileMetadata ReadFileMetadata(const std::string &path) {
  FileMetadata metadata;
  if (path.empty()) {
    return metadata;
  }

#if defined(__linux__) && defined(STATX_BTIME)
  struct statx extended{};
  if (::statx(AT_FDCWD, path.c_str(), 0, STATX_BASIC_STATS | STATX_BTIME,
              &extended) == 0) {
    const bool has_birth = (extended.stx_mask & STATX_BTIME) != 0 &&
                           extended.stx_btime.tv_sec != 0;
    const std::time_t created_seconds =
        has_birth ? static_cast<std::time_t>(extended.stx_btime.tv_sec)
                  : static_cast<std::time_t>(extended.stx_ctime.tv_sec);
    const long created_nanoseconds =
        has_birth ? static_cast<long>(extended.stx_btime.tv_nsec)
                  : static_cast<long>(extended.stx_ctime.tv_nsec);

    FillCommon(metadata, static_cast<std::int64_t>(extended.stx_size),
               static_cast<std::uint32_t>(extended.stx_mode), created_seconds,
               created_nanoseconds, true,
               static_cast<std::time_t>(extended.stx_mtime.tv_sec),
               static_cast<long>(extended.stx_mtime.tv_nsec),
               static_cast<std::time_t>(extended.stx_atime.tv_sec),
               static_cast<long>(extended.stx_atime.tv_nsec));
    return metadata;
  }
#endif

  struct stat status{};
  if (::stat(path.c_str(), &status) != 0) {
    return metadata;
  }

  FillCommon(metadata, static_cast<std::int64_t>(status.st_size),
             static_cast<std::uint32_t>(status.st_mode), status.st_ctime,
  // 老平台没有纳秒字段时按 0 处理。
#if defined(__APPLE__)
             0
#else
             status.st_ctim.tv_nsec
#endif
             ,
             true, status.st_mtime,
#if defined(__APPLE__)
             0
#else
             status.st_mtim.tv_nsec
#endif
             ,
             status.st_atime,
#if defined(__APPLE__)
             0
#else
             status.st_atim.tv_nsec
#endif
  );
  return metadata;
}
} // namespace Posix
} // namespace Everything
} // namespace Lib
