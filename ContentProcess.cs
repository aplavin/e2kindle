using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml;
using e2Kindle.Properties;
using Majestic12;
using NLog;

namespace e2Kindle
{
    public static class ContentProcess
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        const bool MultiThreading = false;
        /// <summary>
        /// Images which have any string from this array in URL will not be downloaded.
        /// </summary>
        static readonly string[] BadImageStrings = { "doubleclick.net/", "feeds.feedburner.com/" };
        /// <summary>
        /// Helper variable to ensure that all images in the resulting FB2 file will have unique names.
        /// </summary>
        static int _uniqueImgNum = 0;

        /// <summary>
        /// Downloads all images from this HTML document (excluding ones those contain any string from BadImageStrings), converts all to base64 and make their names unique. JPG format is used.
        /// </summary>
        /// <param name="htmlContent">HTML code. Image URL must be absolute (start with http:// or smth like this)</param>
        /// <returns>Base64 encoded images as pairs: first is image name, second is base64 encoded image itself.</returns>
        static IEnumerable<KeyValuePair<string, byte[]>> ProcessHtmlImages(ref string htmlContent)
        {
            if (htmlContent == null) throw new ArgumentNullException("htmlContent");

            // this regex matches all img src attributes. Yes, it's not good-looking =)
            var matches = Regex.Matches(htmlContent, @"(?<=img+.+src\=[\x27\x22])(?<Url>[^\x27\x22]*)(?=[\x27\x22])");
            var positions = (from Match match in matches
                             where !BadImageStrings.Any(bad => match.Value.Contains(bad))
                             select Utils.MakePair(match.Index, match.Length));

            var images = new List<KeyValuePair<string, byte[]>>();
            if (positions.Empty()) return images;

            // start from the end 
            foreach (var position in positions.Reverse())
            {
                // get image url from html code
                string url = htmlContent.Substring(position.First, position.Second);

                // download the image
                byte[] image;
                try
                {
                    image = Utils.RecodeImage(url);
                }
                catch (Exception ex)
                {
                    // couldn't recode image - log this and skip
                    logger.WarnException(string.Format("Can't load or convert image from '{0}'.", url), ex);
                    continue;
                }

                // create unique name for image
                string imageName = String.Format("{0}.jpg", Interlocked.Increment(ref _uniqueImgNum));

                // add image to the output dictionary
                images.Add(new KeyValuePair<string, byte[]>(imageName, image));

                // and change image name in the html code
                htmlContent = htmlContent.
                    Remove(position.First, position.Second).
                    Insert(position.First, imageName);
            }

            return images;
        }

        /// <summary>
        /// Parses the HTML code into htmlchunks.
        /// </summary>
        /// <param name="htmlCode"></param>
        /// <returns></returns>
        public static List<HTMLchunk> ParseHTML(string htmlCode)
        {
            if (htmlCode == null) throw new ArgumentNullException("htmlCode");

            var chunks = new List<HTMLchunk>();
            // Note: couldn't find if any exception is thrown during HTML parsing
            using (var parser = new HTMLparser(htmlCode) { bDecodeEntities = true })
            {
                while (true)
                {
                    using (var chunk = parser.ParseNext())
                    {
                        // end of html reached
                        if (chunk == null) break;

                        chunks.Add(chunk.Clone());
                    }
                }
            }
            return chunks;
        }

        /// <summary>
        /// Convert HTMLchunks to FB2 code.
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        static string CombineToFb2(IEnumerable<HTMLchunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException("chunks");

            var result = new StringBuilder();

            foreach (var chunk in chunks)
            {
                switch (chunk.oType)
                {
                    case HTMLchunkType.Text:
                        string text = WebUtility.HtmlEncode(chunk.oHTML);
                        result.Append(text);
                        // space is added due to a bug(?) in calibri with empty text between tags
                        if (!text.Contains(' '))
                            result.Append(' ');
                        break;
                    case HTMLchunkType.OpenTag:
                    case HTMLchunkType.CloseTag:
                        string slashIfClose = (chunk.oType == HTMLchunkType.CloseTag ? "/" : string.Empty);

                        // at 1st process tags that are inserted not depending on type (open/close/endClosure)
                        switch (chunk.sTag)
                        {
                            case "img":
                                // img tags are already processed, so they always have src param and it's correct
                                result.AppendFormat(@"<image l:href=""#{0}""/>", chunk.oParams["src"]);
                                break;
                            case "hr":
                                result.Append("<subtitle>* * *</subtitle>");
                                break;
                            case "br":
                                result.Append("<empty-line/>");
                                break;
                        }

                        // now process tags that are not inserted when endClosure (e.g. <p/>, it can exist on badly formed pages)
                        if (!chunk.bEndClosure)
                            switch (chunk.sTag)
                            {
                                case "strike":
                                case "s":
                                    result.AppendFormat("<{0}strikethrough>", slashIfClose);
                                    break;
                                case "u":
                                    // TODO: try to make correct formatting (underlined)
                                    result.AppendFormat("<{0}emphasis>", slashIfClose);
                                    break;
                                case "b":
                                case "strong":
                                    result.AppendFormat("<{0}strong>", slashIfClose);
                                    break;
                                case "h1":
                                case "h2":
                                case "h3":
                                case "h4":
                                case "h5":
                                case "h6":
                                    result.AppendFormat("<{0}subtitle>", slashIfClose);
                                    break;
                                case "i":
                                case "em":
                                    result.AppendFormat("<{0}emphasis>", slashIfClose);
                                    break;
                                case "code":
                                case "pre":
                                case "tt":
                                    result.AppendFormat("<{0}code>", slashIfClose);
                                    break;
                                // now go tags which name and functionality is the same in HTML and FB2
                                case "sup":
                                case "sub":
                                case "p":
                                case "cite":
                                case "table":
                                case "tr":
                                case "th":
                                case "td":
                                    // append just tag, without its parameters
                                    result.AppendFormat("<{1}{0}>", chunk.sTag, slashIfClose);
                                    break;
                            }
                        break;
                }
            }

            return result.ToString();
        }

        public static string CombineToHtml(this IEnumerable<HTMLchunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException("chunks");

            var result = new StringBuilder();

            foreach (var chunk in chunks)
            {
                switch (chunk.oType)
                {
                    case HTMLchunkType.Text:
                        result.Append(WebUtility.HtmlEncode(chunk.oHTML));
                        break;
                    case HTMLchunkType.OpenTag:
                        result.Append(chunk.GenerateHTML());
                        break;
                    case HTMLchunkType.CloseTag:
                        result.Append(chunk.GenerateHTML());
                        break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Convert HTML code to FB2, including images (they are downloaded, grayscaled and recompressed).
        /// </summary>
        /// <param name="content">Before: HTML code to be converted. After: converted FB2 code.</param>
        /// <param name="binaries">List of binary sections in the FB2 document. This function appends to it.</param>
        static void HtmlToFb2(ref string content, ref List<KeyValuePair<string, byte[]>> binaries)
        {
            if (content == null) throw new ArgumentNullException("content");
            if (binaries == null) throw new ArgumentNullException("binaries");

            // process images in html
            var images = ProcessHtmlImages(ref content);
            // and add them to binaries list
            binaries.AddRange(images);

            List<HTMLchunk> htmlChunks = ParseHTML(content) ?? new List<HTMLchunk>();

            // remove empty texts
            htmlChunks.RemoveAll(c => c.oType == HTMLchunkType.Text && c.oHTML.Trim() == string.Empty);
            // remove comments and scripts
            htmlChunks.RemoveAll(c => c.oType == HTMLchunkType.Comment || c.oType == HTMLchunkType.Script);
            // remove img tags which are not suitable
            htmlChunks.RemoveAll(c => c.sTag == "img" &&
                                        (c.oParams == null || // if it has no params
                                        !c.oParams.ContainsKey("src") || // or has params, but no "src" one
                                        !Regex.IsMatch(c.oParams["src"], @"^\d+.jpe?g$")) // or src isn't in the format {digits}.jp[e]g
            );
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
                    if (i + 1 < htmlChunks.Count &&
                        htmlChunks[i].oType == HTMLchunkType.OpenTag && !htmlChunks[i].bEndClosure &&
                        htmlChunks[i + 1].oType == HTMLchunkType.CloseTag && !htmlChunks[i + 1].bEndClosure &&
                        htmlChunks[i].sTag == htmlChunks[i + 1].sTag)
                    {
                        // remove both open and close tags (order is important)
                        htmlChunks.RemoveAt(i + 1);
                        htmlChunks.RemoveAt(i);
                        i--;
                        changed = true;
                    }
                }
            } while (changed);

            // now combine the htmlchunks converting tags to fb2 ones
            content = CombineToFb2(htmlChunks);
        }

        /// <summary>
        /// Creates FB2 document of all the FeedItems and writes it to the writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="feedEntries"></param>
        public static void CreateFb2(TextWriter writer, IEnumerable<IGrouping<GoogleFeed, GoogleFeedEntry>> feedEntries)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (feedEntries == null) throw new ArgumentNullException("feedEntries");

            logger.Info("Processing {0} feeds entries", feedEntries.Sum(gr => gr.Count()));

            var binaries = new List<KeyValuePair<string, byte[]>>();

            feedEntries.AsParallel().
                ForAll(gr => gr.//AsParallel().
                    ForEach(e =>
                               {
                                   if (Settings.Default.LoadFullContent && FullContent.Exists(e.Link))
                                       e.Content = FullContent.Get(e.Link) ?? (e.Content + "<hr/>[Полное содержание не может быть загружено]");
                                   HtmlToFb2(ref e.Content, ref binaries);
                               }));

            feedEntries = feedEntries.OrderBy(gr => gr.Key.Title);

            logger.Info("Creating XML structure of the FB2 (intermediate format)");
            using (XmlWriter xw = XmlWriter.Create(writer))
            {
                xw.WriteStartDocument();
                xw.WriteStartElement("FictionBook", "http://www.gribuser.ru/xml/fictionbook/2.0");
                xw.WriteAttributeString("xmlns", "l", null, "http://www.w3.org/1999/xlink");

                #region description
                xw.WriteStartElement("description");

                xw.WriteStartElement("title-info");
                xw.WriteElementString("genre", "comp_www");
                xw.WriteRaw(@"<author><first-name>" + DateTime.Now.ToString("dd.MM, dddd, HH:mm") + "</first-name></coverpage>");
                xw.WriteElementString("book-title", "e2Kindle feeds");
                xw.WriteElementString("lang", "ru");
                xw.WriteRaw(@"<coverpage><image l:href=""#cover.png""/></coverpage>");
                xw.WriteElementString("annotation",
                    string.Format("Лент новостей: {0}; записей в них: {1}.", feedEntries.Count(), feedEntries.Sum(gr => gr.Count())));
                xw.WriteEndElement();

                xw.WriteStartElement("document-info");
                xw.WriteRaw(@"<author><nickname>chersanya</nickname></coverpage>");
                xw.WriteElementString("program-used", "e2Kindle");
                xw.WriteElementString("date", DateTime.Now.ToString("yyyy-MM-dd"));
                xw.WriteElementString("id", DateTime.Now.ToString("yyyyMMddHHmmssFF"));
                xw.WriteEndElement();

                xw.WriteEndElement();
                #endregion

                #region body
                xw.WriteStartElement("body");

                foreach (var group in feedEntries)
                {
                    xw.WriteStartElement("section");
                    xw.WriteElementString("title", group.Key.Title);

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
                #endregion

                #region binaries
                foreach (var binary in binaries)
                {
                    xw.WriteStartElement("binary");
                    xw.WriteAttributeString("id", binary.Key);
                    xw.WriteAttributeString("content-type", "image/jpeg");
                    xw.WriteBase64(binary.Value, 0, binary.Value.Length);
                }

                string coverBinary = @"<binary id=""cover.png"" content-type=""image/png"">"
                                     + Convert.ToBase64String(File.ReadAllBytes("Resources/cover.png"))
                                     + "</binary>";
                xw.WriteRaw(coverBinary);
                #endregion

                xw.WriteEndDocument();
            }
        }
    }
}
