/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/posix/wide_string.cpp
 * Brief: UTF-8 与宽字符串互转实现。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 */

#include "wide_string.h"

#include <cstdint>

namespace
{
constexpr std::uint32_t ReplacementCharacter = 0xFFFDU;

// 解码一个 UTF-8 码点。返回消费的字节数；非法序列消费 1 字节并输出 U+FFFD。
std::size_t DecodeOne(const std::string& text, const std::size_t offset, std::uint32_t& codePoint)
{
    const auto byte = static_cast<unsigned char>(text[offset]);
    if (byte < 0x80U)
    {
        codePoint = byte;
        return 1;
    }

    std::size_t length = 0;
    std::uint32_t value = 0;
    if ((byte & 0xE0U) == 0xC0U)
    {
        length = 2;
        value = byte & 0x1FU;
    }
    else if ((byte & 0xF0U) == 0xE0U)
    {
        length = 3;
        value = byte & 0x0FU;
    }
    else if ((byte & 0xF8U) == 0xF0U)
    {
        length = 4;
        value = byte & 0x07U;
    }
    else
    {
        codePoint = ReplacementCharacter;
        return 1;
    }

    if (offset + length > text.size())
    {
        codePoint = ReplacementCharacter;
        return 1;
    }

    for (std::size_t index = 1; index < length; ++index)
    {
        const auto continuation = static_cast<unsigned char>(text[offset + index]);
        if ((continuation & 0xC0U) != 0x80U)
        {
            codePoint = ReplacementCharacter;
            return 1;
        }
        value = (value << 6U) | (continuation & 0x3FU);
    }

    // 拒绝过长编码、代理区与越界码点，避免把无效字节当成合法文字。
    const bool overlong =
        (length == 2 && value < 0x80U) || (length == 3 && value < 0x800U) || (length == 4 && value < 0x10000U);
    if (overlong || value > 0x10FFFFU || (value >= 0xD800U && value <= 0xDFFFU))
    {
        codePoint = ReplacementCharacter;
        return 1;
    }

    codePoint = value;
    return length;
}

void AppendCodePoint(std::wstring& output, const std::uint32_t codePoint)
{
    if (sizeof(wchar_t) >= 4)
    {
        output.push_back(static_cast<wchar_t>(codePoint));
        return;
    }

    // Windows 上 wchar_t 为 16 位，需要 BMP 之外的代理对；
    // 该分支在 POSIX 构建中不会执行，但保持头文件/实现可移植。
    if (codePoint <= 0xFFFFU)
    {
        output.push_back(static_cast<wchar_t>(codePoint));
        return;
    }

    const std::uint32_t value = codePoint - 0x10000U;
    output.push_back(static_cast<wchar_t>(0xD800U + (value >> 10U)));
    output.push_back(static_cast<wchar_t>(0xDC00U + (value & 0x3FFU)));
}

void AppendUtf8(std::string& output, const std::uint32_t codePoint)
{
    if (codePoint < 0x80U)
    {
        output.push_back(static_cast<char>(codePoint));
    }
    else if (codePoint < 0x800U)
    {
        output.push_back(static_cast<char>(0xC0U | (codePoint >> 6U)));
        output.push_back(static_cast<char>(0x80U | (codePoint & 0x3FU)));
    }
    else if (codePoint < 0x10000U)
    {
        output.push_back(static_cast<char>(0xE0U | (codePoint >> 12U)));
        output.push_back(static_cast<char>(0x80U | ((codePoint >> 6U) & 0x3FU)));
        output.push_back(static_cast<char>(0x80U | (codePoint & 0x3FU)));
    }
    else
    {
        output.push_back(static_cast<char>(0xF0U | (codePoint >> 18U)));
        output.push_back(static_cast<char>(0x80U | ((codePoint >> 12U) & 0x3FU)));
        output.push_back(static_cast<char>(0x80U | ((codePoint >> 6U) & 0x3FU)));
        output.push_back(static_cast<char>(0x80U | (codePoint & 0x3FU)));
    }
}
} // namespace

namespace Lib
{
namespace Everything
{
namespace Posix
{
std::wstring WideFromUtf8(const std::string& text)
{
    std::wstring output;
    output.reserve(text.size());
    std::size_t offset = 0;
    while (offset < text.size())
    {
        std::uint32_t codePoint = 0;
        offset += DecodeOne(text, offset, codePoint);
        AppendCodePoint(output, codePoint);
    }
    return output;
}

std::wstring WideFromUtf8(const char* text)
{
    return text != nullptr ? WideFromUtf8(std::string(text)) : std::wstring{};
}

std::string Utf8FromWide(const std::wstring& text)
{
    std::string output;
    output.reserve(text.size());
    std::size_t index = 0;
    while (index < text.size())
    {
        std::uint32_t codePoint = static_cast<std::uint32_t>(text[index]);
        ++index;

        if (sizeof(wchar_t) < 4 && codePoint >= 0xD800U && codePoint <= 0xDBFFU && index < text.size())
        {
            const std::uint32_t low = static_cast<std::uint32_t>(text[index]);
            if (low >= 0xDC00U && low <= 0xDFFFU)
            {
                codePoint = 0x10000U + ((codePoint - 0xD800U) << 10U) + (low - 0xDC00U);
                ++index;
            }
        }

        if (codePoint > 0x10FFFFU)
        {
            codePoint = ReplacementCharacter;
        }
        AppendUtf8(output, codePoint);
    }
    return output;
}
} // namespace Posix
} // namespace Everything
} // namespace Lib
