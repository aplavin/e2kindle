namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    using NLog;

    /// <summary>
    /// Methods working with HTML code or with classe from HtmlParser library.
    /// </summary>
    public static class HtmlUtils
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Images which have any string from this array in URL will not be downloaded.
        /// </summary>
        private static readonly string[] BadImageStrings = { "doubleclick.net/" };

        /// <summary>
        /// Helper variable to ensure that all processed images will have unique names.
        /// </summary>
        private static int _uniqueImgNum = 0;

        /// <summary>
        /// Downloads all images from this HTML document (excluding ones those contain any string from BadImageStrings) and make their names unique numbers. JPG format is used.
        /// </summary>
        /// <param name="htmlContent">HTML code. Image URL must be absolute (start with http:// or smth like this)</param>
        /// <returns>JPEG encoded images as pairs: first is image name, second is image content as byte[].</returns>
        public static IEnumerable<KeyValuePair<string, byte[]>> ProcessHtmlImages(ref string htmlContent)
        {
            if (htmlContent == null) throw new ArgumentNullException("htmlContent");

            // this regex matches all img src attributes. Yes, it's not good-looking =)
            var matches = Regex.Matches(htmlContent, @"(?<=img+.+src\=[\x27\x22])(?<Url>[^\x27\x22]*)(?=[\x27\x22])");
            var positions = from Match match in matches
                            where !BadImageStrings.Any(match.Value.Contains)
                            select Tuple.Create(match.Index, match.Length);

            var images = new List<KeyValuePair<string, byte[]>>();
            if (positions.Empty()) return images;

            // start from the end 
            foreach (var position in positions.Reverse())
            {
                // get image url from html code
                string url = htmlContent.Substring(position.Item1, position.Item2);

                // some heuristics for invalid URLs
                if (url.StartsWith("https://http://")) url = url.Substring(8);
                else if (url.StartsWith("https://https://")) url = url.Substring(8);
                else if (url.StartsWith("http://https://")) url = url.Substring(7);
                else if (url.StartsWith("http://http://")) url = url.Substring(7);

                // download the image
                byte[] image;
                try
                {
                    Bitmap bitmap = ImageUtils.Download(url);
                    bitmap = bitmap.Grayscale().Shrink(800, 800);
                    image = bitmap.GetJpegBytes(40);
                }
                catch (Exception ex)
                {
                    // couldn't recode image - log this and skip
                    logger.WarnException("Can't load or convert image from '{0}'.".FormatWith(url), ex);
                    continue;
                }

                // create unique name for image
                string imageName = "{0}.jpg".FormatWith(Interlocked.Increment(ref _uniqueImgNum));

                // add image to the output dictionary
                images.Add(new KeyValuePair<string, byte[]>(imageName, image));

                // and change image name in the html code
                htmlContent = htmlContent.
                    Remove(position.Item1, position.Item2).
                    Insert(position.Item1, imageName);
            }

            return images;
        }
    }
}