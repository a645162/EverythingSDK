/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/wide_string.h
 * Brief: UTF-8 与宽字符串互转（POSIX 后端内部使用，保持 C++11）。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#pragma once

#include <string>

namespace Lib
{
namespace Everything
{
namespace Posix
{
// 公开 API 统一使用 std::wstring（与 Windows 版本保持一致），
// 而 POSIX 文件系统路径与子进程输出都是 UTF-8 字节串，因此所有
// 边界都需要显式转换。非法字节按 U+FFFD 处理，绝不抛异常，
// 否则一个坏文件名就会让整次搜索失败。
std::wstring WideFromUtf8(const std::string& text);
std::wstring WideFromUtf8(const char* text);
std::string Utf8FromWide(const std::wstring& text);
} // namespace Posix
} // namespace Everything
} // namespace Lib
