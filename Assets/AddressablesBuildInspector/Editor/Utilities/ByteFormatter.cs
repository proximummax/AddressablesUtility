using System;
using System.Globalization;

namespace AddressablesBuildInspector.Editor.Utilities
{
    /// <summary>
    /// Formats byte counts into compact editor-facing text.
    /// </summary>
    public static class ByteFormatter
    {
        private const double Unit = 1024d;

        /// <summary>
        /// Formats a byte count as B, KB, MB, or GB.
        /// </summary>
        /// <param name="bytes">Byte count to format.</param>
        /// <returns>Human-readable size string.</returns>
        public static string FormatBytes(long bytes)
        {
            long safeBytes = Math.Max(0, bytes);
            if (safeBytes < 1024)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} B", safeBytes);
            }

            double value = safeBytes / Unit;
            string suffix = "KB";

            if (value >= Unit)
            {
                value /= Unit;
                suffix = "MB";
            }

            if (value >= Unit)
            {
                value /= Unit;
                suffix = "GB";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1}", value, suffix);
        }
    }
}
