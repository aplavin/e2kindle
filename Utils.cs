using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace e2Kindle
{
    static class Utils
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Downloads image from imageUrl, grayscales, converts and returns it as byte array.
        /// </summary>
        /// <param name="imageUrl"></param>
        public static byte[] RecodeImage(string imageUrl)
        {
            if (imageUrl == null) throw new ArgumentNullException("imageUrl");

            using (var response = WebRequest.Create(imageUrl).GetResponse())
            using (var responseStream = response.GetResponseStream())
            {
                using (var srcBitmap = new Bitmap(responseStream))
                using (var grayBitmap = ImageUtils.Grayscale(srcBitmap))
                using (var shrinkedBitmap = ImageUtils.Shrink(grayBitmap, 800, 800))
                {
                    return ImageUtils.GetJpegData(shrinkedBitmap);
                }
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
        /// Gets response from url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string Download(string url)
        {
            if (url == null) throw new ArgumentNullException("url");

            var request = (HttpWebRequest)WebRequest.Create(url);
            using (var response = request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Precompiled regex to use by Format function.
        /// </summary>
        static readonly Regex CurlyBracesRegex = new Regex(@"(\{+)([^\}]+)(\}+)", RegexOptions.Compiled);

        /// <summary>
        /// Formats arg according to format. It's like string.Format, but uses named parameters.
        /// </summary>
        /// <param name="format">Format string.</param>
        /// <param name="arg">Object to format using format.</param>
        /// <returns>Formatted representation of arg based on format.</returns>
        public static string Format(this string format, object arg)
        {
            if (format == null) throw new ArgumentNullException("format");
            if (arg == null) throw new ArgumentNullException("arg");

            Type type = arg.GetType();
            // replace each regex match with string formed then...
            return CurlyBracesRegex.Replace(format, match =>
            {
                // count of open and close brackets
                int openCount = match.Groups[1].Value.Length;
                int closeCount = match.Groups[3].Value.Length;
                // each 2 brackets are replaced with one in the output, so only remainder matters
                if ((openCount % 2) != (closeCount % 2))
                {
                    throw new ArgumentException("Unbalanced braces in format string.", "format");
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
                var prop = type.GetProperty(fieldName);
                if (prop == null)
                {
                    throw new ArgumentException("Object arg hasn't public field/property which is specified in the format string.", "arg");
                }
                object value = prop.GetValue(arg, null);

                return openBraces + string.Format("{0" + formatString + "}", value);
            });
        }

        /// <summary>
        /// Trims all lines of the string (using string.Trim() method). '\n' and '\r' are supposed to be end of each line.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string TrimLines(this string str)
        {
            if (str == null) throw new ArgumentNullException("str");
            return str.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).
                Select(l => l.Trim()).
                Aggregate(new StringBuilder(), (sb, s) => sb.Append(s), sb => sb.ToString());
        }

        public static bool ContainsCI(this string s, string t)
        {
            return s.ToLower().Contains(t.ToLower());
        }
    }
}
