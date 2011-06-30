using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using FreeImageAPI;

namespace e2Kindle
{
    static class Utils
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Program temporary folder.
        /// </summary>
        public static readonly string TempPath = Path.Combine(Path.GetTempPath(), "e2Kindle");

        /// <summary>
        /// Downloads image from imageUrl, grayscales, converts and returns it as byte array.
        /// </summary>
        /// <param name="imageUrl"></param>
        public static byte[] RecodeImage(string imageUrl)
        {
            try
            {
                using (var response = WebRequest.Create(imageUrl).GetResponse())
                using (var responseStream = response.GetResponseStream())
                {
                    try
                    {
                        var dib = FreeImage.LoadFromStream(responseStream);
                        dib = FreeImage.ConvertToGreyscale(dib);
                        uint h = FreeImage.GetHeight(dib);
                        uint w = FreeImage.GetWidth(dib);
                        if (w > 480)
                        {
                            //dib = FreeImage.Rescale(dib, 480, (int)(h * 480 / w), FREE_IMAGE_FILTER.FILTER_BSPLINE);
                        }
                        using (var memoryStream = new MemoryStream())
                        {
                            //FreeImage.SaveToStream(dib, memoryStream, FREE_IMAGE_FORMAT.FIF_PNG, FREE_IMAGE_SAVE_FLAGS.PNG_Z_BEST_COMPRESSION, FREE_IMAGE_COLOR_DEPTH.FICD_04_BPP);
                            FreeImage.SaveToStream(dib, memoryStream, FREE_IMAGE_FORMAT.FIF_JPEG, FREE_IMAGE_SAVE_FLAGS.JPEG_QUALITYAVERAGE);
                            FreeImage.UnloadEx(ref dib);
                            dib.SetNull();
                            return memoryStream.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Can't convert image '{0}'", imageUrl);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn("Can't download image '{0}'", imageUrl);
                return null;
            }
        }

        /// <summary>
        /// Creates a Pair of specified arguments. Types are automatically detected.
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static Pair<TFirst, TSecond> MakePair<TFirst, TSecond>(TFirst first, TSecond second)
        {
            return new Pair<TFirst, TSecond>(first, second);
        }

        /// <summary>
        /// Creates and returns a temporary directory
        /// </summary>
        public static string CreateTempDirectory()
        {
            string directory;
            do
            {
                directory = Path.Combine(TempPath, Path.GetRandomFileName());
            } while (File.Exists(directory) || Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// Gets response from url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetString(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                using (var response = request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                logger.Error("Can't load URL '{0}'", url);
                throw;
            }
        }

        /// <summary>
        /// Precompiled pattern to use by Format function.
        /// </summary>
        static readonly Regex CurlyBracesRegex = new Regex(@"(\{+)([^\}]+)(\}+)", RegexOptions.Compiled);

        /// <summary>
        /// Formats template according to pattern. It's like string.Format, but uses named parameters.
        /// </summary>
        /// <param name="pattern">Format string.</param>
        /// <param name="template">Object to format using pattern.</param>
        /// <returns>Formatted representation of template based on pattern.</returns>
        public static string Format(this string pattern, object template)
        {
            if (pattern == null) throw new ArgumentNullException("pattern");
            if (template == null) throw new ArgumentNullException("template");

            Type type = template.GetType();
            // replace each regex match with string formed then...
            return CurlyBracesRegex.Replace(pattern, match =>
            {
                // count of open and close brackets
                int openCount = match.Groups[1].Value.Length;
                int closeCount = match.Groups[3].Value.Length;
                // each 2 brackets are replaced with one in the output, so only remainder matters
                if ((openCount % 2) != (closeCount % 2))
                {
                    logger.Error("Unbalanced braces in format string: {0} vs {1}.", openCount, closeCount);
                }
                // create prefix and suffix of the match replacement
                string openBraces = new string('{', openCount / 2);
                string closeBraces = new string('}', closeCount / 2);

                // if all braces are escaped, so the output string between them will be the same as input
                if (openCount % 2 == 0)
                {
                    return openBraces + match.Groups[2].Value + closeBraces;
                }

                // format item, looks like fieldName[,alignment][:formatString]
                string formatItem = match.Groups[2].Value;
                // field/property name of the object
                string fieldName = formatItem.TakeWhile(c => c != ':' && c != ',').Aggregate("", (s, c) => s + c);
                // format string which will be specified in string.Format()
                string formatString = formatItem.SkipWhile(c => c != ':' && c != ',').Aggregate("", (s, c) => s + c);

                // now get the value of object's field/property fieldName
                object value;
                var prop = type.GetProperty(fieldName);
                if (prop == null)
                {
                    logger.Warn("Object hasn't public field/property '{0}' which is specified in the format string.", fieldName);
                    value = null;
                }
                else
                {
                    value = prop.GetValue(template, null);
                }

                return openBraces + string.Format("{0" + formatString + "}", value);
            });
        }

        /// <summary>
        /// Trims all lines of the string (using string.Trim() method). '\n' is supposed to be end of each line.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string TrimLines(this string str)
        {
            return str.Split('\n').
                Select(l => l.Trim()).
                Aggregate((s, l) => s + '\n' + l);
        }
    }
}
