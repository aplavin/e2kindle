namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    using e2Kindle.Properties;

    using GoogleAPI.GoogleReader;

    using Majestic12;

    using NLog;

    public static class ContentProcess
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Images which have any string from this array in URL will not be downloaded.
        /// </summary>
        private static readonly string[] BadImageStrings = { "doubleclick.net/" };

        /// <summary>
        /// Helper variable to ensure that all images in the resulting FB2 file will have unique names.
        /// </summary>
        private static int uniqueImgNum = 0;

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
                            where !BadImageStrings.Any(bad => match.Value.Contains(bad))
                            select Tuple.Create(match.Index, match.Length);

            var images = new List<KeyValuePair<string, byte[]>>();
            if (positions.Empty()) return images;

            // start from the end 
            foreach (var position in positions.Reverse())
            {
                // get image url from html code
                string url = htmlContent.Substring(position.Item1, position.Item2);

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
                    logger.WarnException(string.Format("Can't load or convert image from '{0}'.", url), ex);
                    continue;
                }

                // create unique name for image
                string imageName = string.Format("{0}.jpg", Interlocked.Increment(ref uniqueImgNum));

                // add image to the output dictionary
                images.Add(new KeyValuePair<string, byte[]>(imageName, image));

                // and change image name in the html code
                htmlContent = htmlContent.
                    Remove(position.Item1, position.Item2).
                    Insert(position.Item1, imageName);
            }

            return images;
        }

        /// <summary>
        /// Convert HTML code to FB2, including images (they are downloaded, grayscaled and recompressed).
        /// </summary>
        /// <param name="content">Before: HTML code to be converted. After: converted FB2 code.</param>
        /// <param name="binaries">List of binary sections in the FB2 document. This function appends to it.</param>
        public static string HtmlToFb2(string content, ref List<KeyValuePair<string, byte[]>> binaries)
        {
            if (content == null) throw new ArgumentNullException("content");
            if (binaries == null) throw new ArgumentNullException("binaries");

            // process images in html
            var images = ProcessHtmlImages(ref content);

            // and add them to binaries list
            binaries.AddRange(images);

            var htmlChunks = (HtmlUtils.ParseHtml(content) ?? Enumerable.Empty<HTMLchunk>()).
                WhereNot(c => c.oType == HTMLchunkType.Text && c.oHTML.Trim() == string.Empty). // remove empty texts
                WhereNot(c => c.oType == HTMLchunkType.Comment || c.oType == HTMLchunkType.Script). // remove comments and scripts
                WhereNot(c =>
                    c.sTag == "img" && // remove img tags which are not suitable
                    (c.oParams == null || // if it has no params
                    !c.oParams.ContainsKey("src") || // or has params, but no "src" one
                    !Regex.IsMatch(c.oParams["src"], @"^\d+.jpe?g$"))). // or src isn't in the format {digits}.jp[e]g
                ToList();

            // replace links with just underlined text - links have not much use for us
            htmlChunks.ForEach(c =>
                                   {
                                       if (c.sTag == "a")
                                       {
                                           c.sTag = "u";
                                           c.iParams = 0;
                                           c.oParams.Clear();
                                       }
                                   });

            // remove tags which are opened and closed immediately
            // repeat while we changed something to delete nested tags
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < htmlChunks.Count; i++)
                {
                    if (i + 1 < htmlChunks.Count && htmlChunks[i].oType == HTMLchunkType.OpenTag
                        && !htmlChunks[i].bEndClosure && htmlChunks[i + 1].oType == HTMLchunkType.CloseTag
                        && !htmlChunks[i + 1].bEndClosure && htmlChunks[i].sTag == htmlChunks[i + 1].sTag)
                    {
                        // remove both open and close tags (order is important)
                        htmlChunks.RemoveAt(i + 1);
                        htmlChunks.RemoveAt(i);
                        i--;
                        changed = true;
                    }
                }
            }
            while (changed);

            // now combine the htmlchunks converting tags to fb2 ones
            content = HtmlUtils.CombineToFb2(htmlChunks);

            return content;
        }

        /// <summary>
        /// Creates FB2 document of all the FeedItems and writes it to the writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="feedEntries"></param>
        public static void CreateFb2(TextWriter writer, IEnumerable<FeedEntry> feedEntries)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (feedEntries == null) throw new ArgumentNullException("feedEntries");

            logger.Info("Processing {0} feeds entries", feedEntries.Count());

            var binaries = new List<KeyValuePair<string, byte[]>>();

            var entries = feedEntries.// AsParallel().
                Select(e =>
                {
                    string content = e.Content;
                    if (Settings.Default.LoadFullContent && FullContent.Exists(e.Link))
                    {
                        content = FullContent.Get(e.Link);
                        if (content == null)
                            content += string.Format(
                                "<hr/>[Full article content couldn't be downloaded, although url <u>{0}</u> is supported]",
                                e.Link);
                    }

                    return new
                    {
                        FeedTitle = e.Feed.Title,
                        Title = e.Title,
                        Published = e.Published,
                        Content = HtmlToFb2(content, ref binaries)
                    };
                }).
                GroupBy(e => e.FeedTitle).
                OrderBy(gr => gr.Key).
                ToList();

            logger.Info("Creating XML structure of the FB2 (intermediate format)");
            using (XmlWriter xw = XmlWriter.Create(writer))
            {
                xw.WriteStartDocument();
                xw.WriteStartElement("FictionBook", "http://www.gribuser.ru/xml/fictionbook/2.0");
                xw.WriteAttributeString("xmlns", "l", null, "http://www.w3.org/1999/xlink");

                xw.WriteStartElement("description");

                xw.WriteStartElement("title-info");
                xw.WriteElementString("genre", "comp_www");
                xw.WriteRaw(@"<author><first-name>" + DateTime.Now.ToString("dd.MM - dddd - HH:mm") + "</first-name></author>");
                xw.WriteElementString("book-title", "e2Kindle feeds");
                xw.WriteElementString("lang", "ru");
                xw.WriteRaw(@"<coverpage><image l:href=""#cover.png""/></coverpage>");
                xw.WriteElementString(
                    "annotation",
                    string.Format(
                    "Feeds: {0}; entries: {1}.",
                    entries.Count(),
                    entries.Sum(gr => gr.Count())));
                xw.WriteEndElement();

                xw.WriteStartElement("document-info");
                xw.WriteRaw(@"<author><nickname>chersanya</nickname></author>");
                xw.WriteElementString("program-used", "e2Kindle");
                xw.WriteElementString("date", DateTime.Now.ToString("yyyy-MM-dd"));
                xw.WriteElementString("id", DateTime.Now.ToString("yyyyMMddHHmmssFF"));
                xw.WriteEndElement();

                xw.WriteEndElement();

                xw.WriteStartElement("stylesheet");
                xw.WriteString(".ss {text-decoration: line-through;}");
                xw.WriteString(".u {text-decoration: underline;}");
                xw.WriteString(".i {font-style: italic;}");
                xw.WriteString(".b {font-weight: bold;}");
                xw.WriteEndElement();

                xw.WriteStartElement("body");

                foreach (var group in entries)
                {
                    xw.WriteStartElement("section");
                    xw.WriteElementString("title", group.Key);

                    foreach (var feedEntry in group.OrderByDescending(fi => fi.Published))
                    {
                        xw.WriteStartElement("section");
                        xw.WriteElementString("title", feedEntry.Title);
                        xw.WriteElementString("subtitle", feedEntry.Published.ToString("dd MMMM yyyy (dddd) - HH:mm:ss"));
                        xw.WriteRaw(feedEntry.Content);
                        xw.WriteEndElement();
                    }

                    xw.WriteEndElement();
                }

                xw.WriteEndElement();

                foreach (var binary in binaries)
                {
                    xw.WriteStartElement("binary");
                    xw.WriteAttributeString("id", binary.Key);
                    xw.WriteAttributeString("content-type", "image/jpeg");
                    xw.WriteBase64(binary.Value, 0, binary.Value.Length);
                    xw.WriteEndElement();
                }

                string coverBinary = @"<binary id=""cover.png"" content-type=""image/png"">"
                                     + Convert.ToBase64String(File.ReadAllBytes("Resources/cover.png"))
                                     + "</binary>";
                xw.WriteRaw(coverBinary);

                xw.WriteEndDocument();
            }
        }
    }
}
