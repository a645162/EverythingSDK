/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/process_runner.cpp
 * Brief: POSIX 子进程执行器实现。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#include "process_runner.h"

#include <cerrno>
#include <chrono>
#include <cstdlib>
#include <cstring>

#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>

namespace {
void CloseDescriptor(const int descriptor) {
  if (descriptor >= 0) {
    ::close(descriptor);
  }
}

// waitpid 可能被信号打断，必须循环直到真正拿到状态。
bool WaitForChild(const pid_t pid, int &status) {
  for (;;) {
    const pid_t result = ::waitpid(pid, &status, 0);
    if (result == pid) {
      return true;
    }
    if (result < 0 && errno == EINTR) {
      continue;
    }
    return false;
  }
}
} // namespace

namespace Lib {
namespace Everything {
namespace Posix {
std::string FindExecutable(const std::string &name) {
  if (name.empty()) {
    return {};
  }

  if (name.find('/') != std::string::npos) {
    return ::access(name.c_str(), X_OK) == 0 ? name : std::string{};
  }

  const char *path = std::getenv("PATH");
  if (path == nullptr) {
    return {};
  }

  std::string remaining(path);
  std::size_t begin = 0;
  while (begin <= remaining.size()) {
    const std::size_t end = remaining.find(':', begin);
    const std::string directory = remaining.substr(
        begin, end == std::string::npos ? std::string::npos : end - begin);

    if (!directory.empty()) {
      const std::string candidate = directory + "/" + name;
      if (::access(candidate.c_str(), X_OK) == 0) {
        return candidate;
      }
    }

    if (end == std::string::npos) {
      break;
    }
    begin = end + 1;
  }

  return {};
}

bool FileExists(const std::string &path) {
  struct stat status{};
  return !path.empty() && ::stat(path.c_str(), &status) == 0;
}

ProcessOutcome RunProcess(const std::vector<std::string> &argv,
                          const char delimiter, const int timeout_ms,
                          const RecordConsumer &consumer) {
  ProcessOutcome outcome;
  if (argv.empty()) {
    outcome.error = "empty command";
    return outcome;
  }

  int pipe_descriptors[2] = {-1, -1};
  if (::pipe(pipe_descriptors) != 0) {
    outcome.error = std::string("pipe failed: ") + std::strerror(errno);
    return outcome;
  }

  // fork 与 exec 之间只能调用 async-signal-safe 接口，因此 argv 在 fork 前准备好。
  std::vector<char *> raw_arguments;
  raw_arguments.reserve(argv.size() + 1);
  for (const std::string &argument : argv) {
    raw_arguments.push_back(const_cast<char *>(argument.c_str()));
  }
  raw_arguments.push_back(nullptr);

  const pid_t pid = ::fork();
  if (pid < 0) {
    outcome.error = std::string("fork failed: ") + std::strerror(errno);
    CloseDescriptor(pipe_descriptors[0]);
    CloseDescriptor(pipe_descriptors[1]);
    return outcome;
  }

  if (pid == 0) {
    // 子进程：stdout 接到管道，stderr 丢到 /dev/null，避免错误输出污染结果。
    ::dup2(pipe_descriptors[1], STDOUT_FILENO);
    const int null_descriptor = ::open("/dev/null", O_WRONLY);
    if (null_descriptor >= 0) {
      ::dup2(null_descriptor, STDERR_FILENO);
      ::close(null_descriptor);
    }
    CloseDescriptor(pipe_descriptors[0]);
    CloseDescriptor(pipe_descriptors[1]);
    ::execvp(raw_arguments[0], raw_arguments.data());
    ::_exit(127);
  }

  outcome.started = true;
  CloseDescriptor(pipe_descriptors[1]);

  const int read_descriptor = pipe_descriptors[0];
  std::string pending;
  pending.reserve(4096);
  char buffer[16384];
  bool consumer_stopped = false;
  bool read_failed = false;

  const auto start = std::chrono::steady_clock::now();
  for (;;) {
    int wait_timeout = -1;
    if (timeout_ms > 0) {
      const auto elapsed =
          std::chrono::duration_cast<std::chrono::milliseconds>(
              std::chrono::steady_clock::now() - start);
      if (elapsed.count() >= timeout_ms) {
        outcome.timed_out = true;
        break;
      }
      wait_timeout = static_cast<int>(timeout_ms - elapsed.count());
    }

    struct pollfd descriptor{};
    descriptor.fd = read_descriptor;
    descriptor.events = POLLIN;

    const int ready = ::poll(&descriptor, 1, wait_timeout);
    if (ready < 0) {
      if (errno == EINTR) {
        continue;
      }
      read_failed = true;
      break;
    }
    if (ready == 0) {
      outcome.timed_out = true;
      break;
    }

    const ssize_t count = ::read(read_descriptor, buffer, sizeof(buffer));
    if (count < 0) {
      if (errno == EINTR) {
        continue;
      }
      read_failed = true;
      break;
    }
    if (count == 0) {
      break;
    }

    pending.append(buffer, static_cast<std::size_t>(count));

    std::size_t begin = 0;
    for (;;) {
      const std::size_t end = pending.find(delimiter, begin);
      if (end == std::string::npos) {
        break;
      }

      ++outcome.record_count;
      const bool keep_going =
          !consumer || consumer(pending.substr(begin, end - begin));
      begin = end + 1;
      if (!keep_going) {
        consumer_stopped = true;
        break;
      }
    }

    if (begin > 0) {
      pending.erase(0, begin);
    }

    if (consumer_stopped) {
      break;
    }
  }

  if (consumer_stopped || outcome.timed_out || read_failed) {
    // 调用方不再需要更多记录（或已经超时）：先礼后兵，避免 find 在巨大目录里继续跑。
    ::kill(pid, SIGTERM);
  }

  CloseDescriptor(read_descriptor);

  // 最后一段没有分隔符的内容也要交给消费方，否则会漏掉唯一一条结果。
  if (!pending.empty() && !consumer_stopped && !outcome.timed_out &&
      !read_failed) {
    ++outcome.record_count;
    if (consumer) {
      consumer(pending);
    }
  }

  int status = 0;
  if (WaitForChild(pid, status)) {
    if (WIFEXITED(status)) {
      outcome.completed = true;
      outcome.exit_code = WEXITSTATUS(status);
    } else if (WIFSIGNALED(status)) {
      outcome.exit_code = 128 + WTERMSIG(status);
    }
  } else {
    outcome.error = std::string("waitpid failed: ") + std::strerror(errno);
  }

  if (consumer_stopped) {
    outcome.truncated = true;
  } else if (read_failed && outcome.error.empty()) {
    outcome.error = std::string("read failed: ") + std::strerror(errno);
  }

  return outcome;
}
} // namespace Posix
} // namespace Everything
} // namespace Lib
