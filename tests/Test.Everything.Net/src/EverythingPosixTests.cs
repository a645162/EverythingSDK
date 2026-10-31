// File: libs/EverythingSDK/tests/Test.Everything.Net/src/EverythingPosixTests.cs
// Brief: POSIX 后端测试：强制 scan 后端，覆盖语法、排序、分页与元数据。
// Author: Haomin Kong
// Email: a645162@163.com
// Created: 2026-10-31

using Lib.Everything;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Test.Everything.Net
{
    [TestClass]
    public class EverythingPosixTests
    {
        private string root;

        [TestInitialize]
        public void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("POSIX 后端测试仅在 Linux/macOS 上运行。");
            }

            root = Path.Combine(Path.GetTempPath(), "everything-posix-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllText(Path.Combine(root, "alpha.PNG"), "test");
            File.WriteAllText(Path.Combine(root, "beta.png"), "test");
            File.WriteAllText(Path.Combine(root, "gamma.jpg"), "test");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "test");
            File.WriteAllText(Path.Combine(root, "sub", "delta.png"), "test");

            // 大小与内容过滤需要可区分的样本：其余小文件都是 4 字节。
            File.WriteAllBytes(Path.Combine(root, "big.bin"), new byte[8 * 1024]);
            File.WriteAllText(Path.Combine(root, "tiny.bin"), "0123456789");
            File.WriteAllText(Path.Combine(root, "report.txt"),
                "nothing to see, then a needle appears");

            // 用进程内扫描保证测试不依赖本机 locate 数据库内容。
            Environment.SetEnvironmentVariable("EVERYTHING_SDK_SEARCH_BACKEND", "scan");
            Environment.SetEnvironmentVariable("EVERYTHING_SDK_SEARCH_ROOTS", root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable("EVERYTHING_SDK_SEARCH_BACKEND", null);
            Environment.SetEnvironmentVariable("EVERYTHING_SDK_SEARCH_ROOTS", null);
            if (root != null && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Query_ByExtension_ReturnsMatchingFiles()
        {
            using (var client = new EverythingClient())
            {
                client.SearchText = "ext:png";
                client.MatchPath = true;
                client.Sort = EverythingSort.NameAscending;

                List<EverythingResult> results = client.Query();

                Assert.AreEqual(3, results.Count);
                Assert.AreEqual(3u, client.TotalResults);
                Assert.IsTrue(Contains(results, "alpha.PNG"));
                Assert.IsTrue(Contains(results, "beta.png"));
                Assert.IsTrue(Contains(results, "delta.png"));
                Assert.AreEqual(EverythingError.Ok, client.LastError);
            }
        }

        [TestMethod]
        public void Query_HonoursOffsetAndMax()
        {
            using (var client = new EverythingClient())
            {
                client.SearchText = "ext:png";
                client.MatchPath = true;
                client.Sort = EverythingSort.NameDescending;
                client.Max = 2;
                client.Offset = 0;

                List<EverythingResult> firstPage = client.Query();
                Assert.AreEqual(2, firstPage.Count);
                Assert.AreEqual(3u, client.TotalResults);

                client.Offset = 2;
                List<EverythingResult> secondPage = client.Query();
                Assert.AreEqual(1, secondPage.Count);
            }
        }

        [TestMethod]
        public void Query_SupportsNegationAndMetadata()
        {
            using (var client = new EverythingClient())
            {
                client.SearchText = "ext:png !alpha";
                client.MatchPath = true;
                client.Sort = EverythingSort.NameAscending;

                List<EverythingResult> results = client.Query();
                Assert.AreEqual(2, results.Count);
                Assert.IsFalse(Contains(results, "alpha.PNG"));

                client.SearchText = "ext:txt";
                List<EverythingResult> textFiles = client.Query();
                Assert.AreEqual(2, textFiles.Count);
                Assert.IsTrue(textFiles[0].IsFile);
                Assert.IsFalse(textFiles[0].IsFolder);
                Assert.IsTrue(textFiles[0].Size.HasValue);
                Assert.IsTrue(textFiles[0].DateModified.HasValue);
            }
        }

        [TestMethod]
        public void Query_SupportsSizeDateAndContentFilters()
        {
            using (var client = new EverythingClient())
            {
                client.MatchPath = true;

                // size: 比较与区间
                client.SearchText = "size:>1kb";
                List<EverythingResult> large = client.Query();
                Assert.AreEqual(1, large.Count, "size:>1kb 只应命中 big.bin");
                Assert.IsTrue(Contains(large, "big.bin"));

                client.SearchText = "size:<20";
                List<EverythingResult> small = client.Query();
                // 6 个 4 字节的小文件（含 tiny.bin 的 10 字节）+ 目录 sub（按 0 计）。
                Assert.AreEqual(7, small.Count, "size:<20 应命中所有小文件与目录");
                Assert.IsTrue(Contains(small, "tiny.bin"));

                client.SearchText = "size:1kb..16kb";
                List<EverythingResult> ranged = client.Query();
                Assert.AreEqual(1, ranged.Count);
                Assert.IsTrue(Contains(ranged, "big.bin"));

                client.SearchText = "* !size:<1kb";
                List<EverythingResult> negatedSize = client.Query();
                Assert.AreEqual(1, negatedSize.Count, "!size:<1kb 只应留下 big.bin");

                // dm:/dc: 时间过滤（样本刚创建，用 1990 年做分界）
                client.SearchText = "dm:>1990-01-01";
                Assert.AreEqual(9, client.Query().Count, "dm:>1990-01-01 应命中全部条目");

                client.SearchText = "dm:<1990-01-01";
                Assert.AreEqual(0, client.Query().Count);

                client.SearchText = "dc:>=1990-01-01";
                Assert.AreEqual(9, client.Query().Count);

                client.SearchText = "dm:1990-01-01..1991-01-01";
                Assert.AreEqual(0, client.Query().Count);

                // content: 只读文件开头，命中 needle
                client.SearchText = "content:needle";
                List<EverythingResult> contents = client.Query();
                Assert.AreEqual(1, contents.Count);
                Assert.IsTrue(Contains(contents, "report.txt"));

                client.SearchText = "content:needle !report";
                Assert.AreEqual(0, client.Query().Count);
            }
        }

        [TestMethod]
        public void Query_SupportsWildcards()
        {
            using (var client = new EverythingClient())
            {
                client.SearchText = "*.png";
                client.Sort = EverythingSort.NameAscending;

                List<EverythingResult> results = client.Query();
                Assert.AreEqual(3, results.Count);
                Assert.IsTrue(Contains(results, "alpha.PNG"));
            }
        }

        [TestMethod]
        public void Query_WithFdBackend_WhenAvailable()
        {
            string fd = FindOnPath("fd") ?? FindOnPath("fdfind");
            if (fd == null)
            {
                Assert.Inconclusive("本机未安装 fd/fdfind，跳过 fd 后端测试。");
            }

            Environment.SetEnvironmentVariable("EVERYTHING_SDK_SEARCH_BACKEND", "fd");

            using (var client = new EverythingClient())
            {
                client.SearchText = "ext:png";
                client.MatchPath = true;

                List<EverythingResult> results = client.Query();
                Assert.AreEqual(3, results.Count);
                Assert.AreEqual("fd", EverythingClient.GetBackendName());

                client.SearchText = "*.png";
                List<EverythingResult> globResults = client.Query();
                Assert.AreEqual(3, globResults.Count);
                Assert.IsTrue(Contains(globResults, "alpha.PNG"));
            }
        }

        [TestMethod]
        public void BackendName_IsReported()
        {
            Assert.IsFalse(string.IsNullOrEmpty(EverythingClient.GetBackendName()));
            Assert.IsTrue(EverythingClient.IsEverythingRunning());
        }

        [TestMethod]
        public void AvailableBackends_AlwaysContainsScan()
        {
            string[] backends = EverythingClient.AvailableBackends();

            Assert.IsNotNull(backends);
            Assert.IsTrue(backends.Length > 0);
            CollectionAssert.Contains(backends, "scan");
        }

        [TestMethod]
        public void PreferredBackend_ChangesActiveBackendAndCanBeCleared()
        {
            try
            {
                EverythingClient.SetPreferredBackend("find");

                Assert.AreEqual("find", EverythingClient.GetPreferredBackend());
                Assert.AreEqual("find", EverythingClient.GetBackendName());

                EverythingClient.SetPreferredBackend(string.Empty);

                Assert.AreEqual(string.Empty, EverythingClient.GetPreferredBackend());
            }
            finally
            {
                EverythingClient.SetPreferredBackend(string.Empty);
            }
        }

        private static bool Contains(IEnumerable<EverythingResult> results, string fileName)
        {
            foreach (EverythingResult result in results)
            {
                if (string.Equals(result.FileName, fileName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string FindOnPath(string name)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (string directory in path.Split(':'))
            {
                if (directory.Length == 0)
                {
                    continue;
                }
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
