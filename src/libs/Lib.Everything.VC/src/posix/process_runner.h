/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/process_runner.h
 * Brief: POSIX 子进程执行器：按记录边界流式读取 stdout，供 locate/find 后端复用。
 * Author: Haomin Kong
 * Email: a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include <cstddef>
#include <functional>
#include <string>
#include <vector>

namespace Lib {
namespace Everything {
namespace Posix {
struct ProcessOutcome {
  bool started{false};
  bool completed{false};
  bool truncated{false};
  bool timed_out{false};
  int exit_code{-1};
  std::size_t record_count{0};
  std::string error;
};

// 消费一条记录；返回 false 表示调用方已收集足够结果，执行器会终止子进程。
using RecordConsumer = std::function<bool(const std::string &)>;

// 直接 fork/exec，不经过 shell，避免路径里的空格、引号与 $ 被解释。
// delimiter 通常是 '\0'（-print0 / -0）或 '\n'。
// timeout_ms <= 0 表示不限时（但仍受调用方关闭管道的控制）。
ProcessOutcome RunProcess(const std::vector<std::string> &argv, char delimiter,
                          int timeout_ms, const RecordConsumer &consumer);

// 在 PATH 中查找可执行文件，返回绝对路径；找不到返回空串。
std::string FindExecutable(const std::string &name);

bool FileExists(const std::string &path);
} // namespace Posix
} // namespace Everything
} // namespace Lib
