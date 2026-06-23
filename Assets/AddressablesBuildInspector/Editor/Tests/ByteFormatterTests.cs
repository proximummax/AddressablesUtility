using AddressablesBuildInspector.Editor.Utilities;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class ByteFormatterTests
    {
        [Test]
        public void FormatBytes_FormatsBytesBelowOneKilobyte()
        {
            Assert.AreEqual("512 B", ByteFormatter.FormatBytes(512));
        }

        [Test]
        public void FormatBytes_FormatsKilobytes()
        {
            Assert.AreEqual("1.50 KB", ByteFormatter.FormatBytes(1536));
        }

        [Test]
        public void FormatBytes_FormatsMegabytes()
        {
            Assert.AreEqual("2.00 MB", ByteFormatter.FormatBytes(2L * 1024L * 1024L));
        }

        [Test]
        public void FormatBytes_FormatsGigabytes()
        {
            Assert.AreEqual("3.25 GB", ByteFormatter.FormatBytes(3489660928L));
        }

        [Test]
        public void FormatBytes_ClampsNegativeValuesToZero()
        {
            Assert.AreEqual("0 B", ByteFormatter.FormatBytes(-42));
        }
    }
}
