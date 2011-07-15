namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Methods to work with Calibre using command line.
    /// </summary>
    public class Calibre
    {
        /// <summary>
        /// Converts inputFile to format (extension) outputFormat using command ebook-convert from Calibre.
        /// </summary>
        public static void Convert(string inputFile, string outputFormat)
        {
            if (inputFile == null) throw new ArgumentNullException("inputFile");
            if (outputFormat == null) throw new ArgumentNullException("outputFormat");

            string outputFile = Path.ChangeExtension(inputFile, outputFormat.ToLower());
            if (inputFile == outputFile) return;

            if (!File.Exists(inputFile)) throw new FileNotFoundException("Input file doesn't exist.", inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);

            try
            {
                try
                {
                    if (ProcessUtils.RunProcess("ebook-convert", inputFile, outputFile) != 0)
                    {
                        throw new FileFormatException(
                            string.Format(
                                "Format (extension) of input (*{0}) or output (*{1}) file isn't supported by Calibre.",
                                Path.GetExtension(inputFile),
                                Path.GetExtension(outputFile)));
                    }
                }
                catch (Win32Exception ex)
                {
                    throw new FileNotFoundException("Calibre isn't installed properly", "ebook-convert", ex);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Can't convert '{0}' to '{1}'.", inputFile, outputFile), ex);
            }
        }

        /// <summary>
        /// Converts inputFile to all formats (extensions) in the outputFormats list using command ebook-convert from Calibre.
        /// </summary>
        public static void Convert(string inputFile, IEnumerable<string> outputFormats)
        {
            outputFormats.AsParallel().
                ForAll(f => Convert(inputFile, f));
        }
    }
}
