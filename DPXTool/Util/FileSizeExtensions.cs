namespace DPXTool.Util
{
    /// <summary>
    /// provides extension methods for long, useful for formatting as a file size
    /// </summary>
    public static class FileSizeExtensions
    {
        /// <summary>
        /// get the file size as a string with unit (xx MB)
        /// </summary>
        /// <param name="bytes">the number of bytes</param>
        /// <returns>the file size string</returns>
        public static string ToFileSize(this long bytes)
        {
            return ((double)bytes).ToDataSize();
        }

        /// <summary>
        /// get the file size as a string with unit (xx MB)
        /// </summary>
        /// <param name="bytes">the number of bytes</param>
        /// <returns>the file size string</returns>
        public static string ToDataSize(this double bytes)
        {
            // data units, each is 1000x bigger than the one before
            string[] UNITS = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

            //find what size we should use
            double size = bytes;
            int unit = 0;
            while (size >= 1000 && unit < (UNITS.Length - 1))
            {
                unit++;
                size /= 1000;
            }

            return $"{size:0.##} {UNITS[unit]}";
        }
    }
}
