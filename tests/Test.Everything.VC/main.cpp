/*
 * File: libs/EverythingSDK/tests/Test.Everything.VC/main.cpp
 * Brief: POSIX 后端端到端测试：语法、排序、分页、元数据与后端名称。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#include <Lib/Everything/Everything.h>

#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <string>
#include <vector>

#include <sys/stat.h>
#include <unistd.h>

namespace {
int failures = 0;

void Check(const bool condition, const char *message) {
  if (condition) {
    return;
  }

  ++failures;
  std::printf("FAIL: %s\n", message);
}

void WriteFile(const std::string &directory, const std::string &name) {
  const std::string path = directory + "/" + name;
  std::ofstream stream(path, std::ios::binary);
  stream << "Everything POSIX test";
}

void MakeDirectory(const std::string &path) { ::mkdir(path.c_str(), 0700); }

// 测试机器不一定装了 fd，涉及 fd 的断言必须先探测，避免把环境差异当成缺陷。
bool ExecutableInPath(const char *name) {
  const char *path = std::getenv("PATH");
  if (path == nullptr) {
    return false;
  }

  std::string remaining(path);
  std::size_t begin = 0;
  for (;;) {
    const std::size_t end = remaining.find(':', begin);
    const std::string directory = remaining.substr(
        begin, end == std::string::npos ? std::string::npos : end - begin);
    if (!directory.empty()) {
      const std::string candidate = directory + "/" + name;
      if (::access(candidate.c_str(), X_OK) == 0) {
        return true;
      }
    }
    if (end == std::string::npos) {
      return false;
    }
    begin = end + 1;
  }
}

std::wstring Widen(const std::string &text) {
  std::wstring output;
  output.reserve(text.size());
  for (const char value : text) {
    output.push_back(static_cast<wchar_t>(static_cast<unsigned char>(value)));
  }
  return output;
}

bool Contains(const std::vector<Lib::Everything::EverythingResult> &results,
              const std::string &name) {
  const std::wstring needle = Widen(name);
  for (const Lib::Everything::EverythingResult &result : results) {
    if (result.FileName == needle) {
      return true;
    }
  }
  return false;
}
} // namespace

int main() {
  // 用进程内扫描保证测试不依赖本机 locate 数据库内容。
  char template_directory[] = "/tmp/everything-sdk-test-XXXXXX";
  const char *created = ::mkdtemp(template_directory);
  if (created == nullptr) {
    std::printf("FAIL: mkdtemp\n");
    return 2;
  }

  const std::string root(created);
  MakeDirectory(root + "/sub");
  WriteFile(root, "alpha.PNG");
  WriteFile(root, "beta.png");
  WriteFile(root, "gamma.jpg");
  WriteFile(root, "notes.txt");
  WriteFile(root + "/sub", "delta.png");

  // 大小与内容过滤需要可区分的样本：其余文件都是 22 字节。
  {
    std::ofstream big(root + "/big.bin", std::ios::binary);
    big << std::string(8U * 1024U, 'x');
  }
  {
    std::ofstream tiny(root + "/tiny.bin", std::ios::binary);
    tiny << "0123456789";
  }
  {
    std::ofstream report(root + "/report.txt", std::ios::binary);
    report << "nothing to see, then a needle appears";
  }

  ::setenv("EVERYTHING_SDK_SEARCH_BACKEND", "scan", 1);
  ::setenv("EVERYTHING_SDK_SEARCH_ROOTS", root.c_str(), 1);

  {
    using namespace Lib::Everything;

    EverythingClient client;
    client.SearchText = L"ext:png";
    client.MatchPath = true;
    client.Sort = EverythingSort::NameAscending;
    const std::vector<EverythingResult> results = client.Query();
    Check(results.size() == 3, "ext:png should return three png files");
    Check(Contains(results, "alpha.PNG"), "alpha.PNG should be found");
    Check(Contains(results, "beta.png"), "beta.png should be found");
    Check(Contains(results, "delta.png"), "delta.png should be found");
    Check(client.TotalResults == 3, "TotalResults should be three");

    // 排序 + 分页
    client.SearchText = L"ext:png";
    client.Sort = EverythingSort::NameDescending;
    client.Max = 2;
    client.Offset = 0;
    const std::vector<EverythingResult> first_page = client.Query();
    Check(first_page.size() == 2, "Max=2 should limit the page to two entries");
    Check(client.TotalResults == 3, "TotalResults must not be limited by Max");
    client.Offset = 2;
    const std::vector<EverythingResult> second_page = client.Query();
    Check(second_page.size() == 1,
          "Offset=2 should return the remaining entry");

    // 否定与短语
    client.Max = 0;
    client.Offset = 0;
    client.SearchText = L"ext:png !alpha";
    client.Sort = EverythingSort::NameAscending;
    const std::vector<EverythingResult> negated = client.Query();
    Check(negated.size() == 2, "!alpha should exclude alpha.PNG");
    Check(!Contains(negated, "alpha.PNG"), "alpha.PNG must be excluded");

    // 通配符（Everything 默认语法）
    client.SearchText = L"*.png";
    client.Max = 0;
    client.Offset = 0;
    const std::vector<EverythingResult> wildcard = client.Query();
    Check(wildcard.size() == 3, "*.png should match three png files");

    // 元数据
    client.SearchText = L"ext:txt";
    const std::vector<EverythingResult> text_files = client.Query();
    Check(text_files.size() == 2, "ext:txt should return two text files");
    if (!text_files.empty()) {
      Check(text_files.front().IsFile, "txt result should be a file");
      Check(!text_files.front().IsFolder, "txt result must not be a folder");
      Check(text_files.front().HasSize, "size should be reported");
    }

    // size: 比较与区间
    client.SearchText = L"size:>1kb";
    const std::vector<EverythingResult> large = client.Query();
    Check(large.size() == 1, "size:>1kb should return only big.bin");
    Check(Contains(large, "big.bin"), "size:>1kb should include big.bin");

    client.SearchText = L"size:<20";
    const std::vector<EverythingResult> small = client.Query();
    // tiny.bin 是 10 字节；目录 sub 按 0 字节计，因此这里应有两个条目。
    Check(small.size() == 2, "size:<20 should return tiny.bin and the folder");
    Check(Contains(small, "tiny.bin"), "size:<20 should include tiny.bin");

    client.SearchText = L"size:1kb..16kb";
    const std::vector<EverythingResult> ranged = client.Query();
    Check(ranged.size() == 1, "size:1kb..16kb should return only big.bin");
    Check(Contains(ranged, "big.bin"), "size range should include big.bin");

    client.SearchText = L"* !size:<1kb";
    const std::vector<EverythingResult> negated_size = client.Query();
    Check(negated_size.size() == 1, "!size:<1kb should leave only big.bin");

    // dm:/dc: 时间过滤（样本刚创建，用 1990 年做分界）
    // 命中数是 8 个文件 + 1 个子目录。
    client.SearchText = L"dm:>1990-01-01";
    const std::vector<EverythingResult> recent = client.Query();
    Check(recent.size() == 9, "dm:>1990-01-01 should match every entry");

    client.SearchText = L"dm:<1990-01-01";
    Check(client.Query().empty(), "dm:<1990-01-01 should match nothing");

    client.SearchText = L"dc:>=1990-01-01";
    Check(client.Query().size() == 9,
          "dc:>=1990-01-01 should match every entry");

    client.SearchText = L"dm:1990-01-01..1991-01-01";
    Check(client.Query().empty(), "dm range in the past should match nothing");

    // content: 只读文件开头，命中 needle
    client.SearchText = L"content:needle";
    const std::vector<EverythingResult> contents = client.Query();
    Check(contents.size() == 1, "content:needle should return report.txt");
    Check(Contains(contents, "report.txt"),
          "content:needle should include report.txt");

    client.SearchText = L"content:needle !report";
    Check(client.Query().empty(),
          "content:needle !report should match nothing");

    Check(!EverythingClient::GetBackendName().empty(),
          "backend name should not be empty");
    Check(EverythingClient::IsEverythingRunning(),
          "POSIX backend should report available");

    // 可用后端列表至少要包含永远可用的 scan。
    const std::vector<std::wstring> available =
        EverythingClient::AvailableBackends();
    Check(!available.empty(), "available backend list must not be empty");
    bool has_scan = false;
    for (const std::wstring &name : available) {
      if (name == L"scan") {
        has_scan = true;
      }
    }
    Check(has_scan, "available backends must always include scan");

    // 手动指定后端：设置后应立即生效，清空后回到自动。
    EverythingClient::SetPreferredBackend(L"find");
    Check(EverythingClient::GetPreferredBackend() == L"find",
          "preferred backend should be reported back");
    Check(EverythingClient::GetBackendName() == L"find",
          "preferred backend should be used by the next query");

    EverythingClient::SetPreferredBackend(L"");
    Check(EverythingClient::GetPreferredBackend().empty(),
          "empty preference should mean automatic");
  }

  // fd 后端：可用时验证后端名称与实际结果，未安装则跳过。
  if (ExecutableInPath("fd") || ExecutableInPath("fdfind")) {
    ::setenv("EVERYTHING_SDK_SEARCH_BACKEND", "fd", 1);
    using namespace Lib::Everything;

    Check(EverythingClient::GetBackendName() == L"fd",
          "forced fd backend should be reported by GetBackendName");

    EverythingClient fd_client;
    fd_client.SearchText = L"ext:png";
    fd_client.MatchPath = true;
    const std::vector<EverythingResult> by_extension = fd_client.Query();
    Check(by_extension.size() == 3, "fd backend should return three png files");

    fd_client.SearchText = L"*.png";
    const std::vector<EverythingResult> by_glob = fd_client.Query();
    Check(by_glob.size() == 3, "fd backend should honour * wildcards");

    // 恢复 scan，避免后续逻辑受环境影响。
    ::setenv("EVERYTHING_SDK_SEARCH_BACKEND", "scan", 1);
  }

  // 清理
  ::unlink((root + "/alpha.PNG").c_str());
  ::unlink((root + "/beta.png").c_str());
  ::unlink((root + "/gamma.jpg").c_str());
  ::unlink((root + "/notes.txt").c_str());
  ::unlink((root + "/sub/delta.png").c_str());
  ::rmdir((root + "/sub").c_str());
  ::rmdir(root.c_str());

  if (failures == 0) {
    std::printf("[PASS] Test.Everything.VC\n");
    return 0;
  }

  std::printf("%d check(s) failed\n", failures);
  return 1;
}
