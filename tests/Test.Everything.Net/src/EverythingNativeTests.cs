using Lib.Everything;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.InteropServices;

namespace Test.Everything.Net
{
    [TestClass]
    public class EverythingNativeTests
    {
        [DataTestMethod]
        [DataRow(Architecture.X86, "Everything32.dll")]
        [DataRow(Architecture.X64, "Everything64.dll")]
        [DataRow(Architecture.Arm, "EverythingARM.dll")]
        [DataRow(Architecture.Arm64, "EverythingARM64.dll")]
        public void GetDllFileNameForArchitecture_ReturnsExpected(Architecture architecture, string expected)
        {
            Assert.AreEqual(expected, EverythingNative.GetDllFileNameForArchitecture(architecture));
        }

        [TestMethod]
        public void PtrToStringUni_ReturnsNull_ForZero()
        {
            Assert.IsNull(EverythingNative.PtrToStringUni(IntPtr.Zero));
        }
    }
}
