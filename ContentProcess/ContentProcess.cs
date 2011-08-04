namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml;
    using e2Kindle.Properties;
    using GoogleAPI.GoogleReader;
    using HtmlParserMajestic.Wrappers;
    using NLog;

    public static class ContentProcess
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly string[] Styles;

        static ContentProcess()
        {
            var stylesTemp = new List<string> { 
                ".s {text-decoration: line-through;}", 
                ".u {text-decoration: underline;}",
                ".i {font-style: italic;}",
                ".b {font-weight: bold;}",
                ".mono {font-family: monospace;}",
                ".small {font-size: small;}",
                ".large {font-size: large;}",
                ".xlarge {font-size: x-large;}",
                ".xxlarge {font-size: xx-large;}",
                ".scaps {font-variant: small-caps;}",
            };

            for (int i = 0; i <= 0xF; i++)
            {
                char col = i.ToString("X")[0];
                string color = new string(col, 6);
                stylesTemp.Add(".col{0} {{color: #{1};}}".FormatWith(char.ToLower(col), color));
            }

            Styles = stylesTemp.ToArray();
        }

        /// <summary>
        /// Convert HTML code to FB2, including images (they are downloaded, grayscaled and recompressed).
        /// </summary>
        /// <param name="content">Before: HTML code to be converted. After: converted FB2 code.</param>
        /// <param name="withImages"></param>
        /// <param name="binaries">List of binary sections in the FB2 document. This function appends to it.</param>
        private static string HtmlToFb2(string content, bool withImages, List<KeyValuePair<string, byte[]>> binaries)
        {
            if (content == null) throw new ArgumentNullException("content");
            if (binaries == null) throw new ArgumentNullException("binaries");

            // process images in html
            var images = withImages ?
                HtmlUtils.ProcessHtmlImages(ref content) :
                Enumerable.Empty<KeyValuePair<string, byte[]>>();

            // and add them to binaries list
            binaries.AddRange(images);

            var htmlChunks =
                HtmlParser.Parse(content).
                WhereNot(c => c.Type == HtmlChunkType.Text && c.Text.IsNullOrWhiteSpace() && !c.Text.Contains(' ')). // remove empty texts
                WhereNot(c => c.Type.IsOneOf(HtmlChunkType.Comment, HtmlChunkType.Script)). // remove comments and scripts
                WhereNot(c =>
                    c.TagName == "img" && // remove img tags which are not suitable
                    !c.ParameterMatches("src", new Regex(@"^\d+.jpe?g$"))). // which src isn't in the format {digits}.jp[e]g
                    ToList();

            // remove tags which are opened and closed immediately
            // repeat while we changed something to delete nested tags
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < htmlChunks.Count; i++)
                {
                    if (i + 1 < htmlChunks.Count &&
                        htmlChunks[i].TagType == HtmlTagType.Open &&
                        htmlChunks[i + 1].TagType == HtmlTagType.Close &&
                        htmlChunks[i].TagName == htmlChunks[i + 1].TagName)
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
            content = Fb2Utils.Combine(htmlChunks);

            return content;
        }

        /// <summary>
        /// Creates FB2 document of all the FeedItems and writes it to the writer.
        /// </summary>
        public static void CreateFb2(
            TextWriter writer,
            IEnumerable<Feed> feeds,
            Dictionary<Feed, FeedSettings> feedSettings,
            Action<int, string> callback = null)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (feeds == null) throw new ArgumentNullException("feeds");
            feeds = feeds.ToList(); // enumerate once
            if (feedSettings == null) throw new ArgumentNullException("feedSettings");
            if (!feeds.All(feedSettings.ContainsKey)) throw new ArgumentException("Feeds settings doesn't contain settings for all feeds.");
            if (callback == null) callback = (i, s) => { }; // do null check once

            feeds = feeds.Where(f => feedSettings[f].Enabled);

            logger.Info("Loading feeds entries");
            callback(0, "Loading feeds entries...");
            var feedEntries = feeds.AsParallel().SelectMany(f => GoogleReader.GetEntries(f, feedSettings[f].LoadAllEntries ? 1000 : 0)).ToList();
            logger.Info("Loaded feeds entries");

            var binaries = new List<KeyValuePair<string, byte[]>>();

            int cnt = 0;
            int max = feedEntries.Count();

            var entries = feedEntries
                .AsParallel()
                .Select(fe =>
                    {
                        callback(cnt * 100 / max, "Processing entry {0}/{1}...".FormatWith(Interlocked.Increment(ref cnt), max));

                        var feedSetting = feedSettings[fe.Feed];

                        string content;
                        if (feedSetting.FullContent)
                        {
                            content = FullContent.Exists(fe.Link)
                                ? FullContent.Get(fe.Link)
                                : GenericFullContent.Get(fe.Link);

                            if (content.IsNullOrWhiteSpace())
                            {
                                content = "{0}<hr/>[Full article content couldn't be downloaded, URL: <a>{1}</a>]"
                                    .FormatWith(fe.Content, fe.Link);
                            }
                        }
                        else
                        {
                            content = fe.Content;
                        }

                        return new
                        {
                            FeedTitle = fe.Feed.Title,
                            Title = fe.Title,
                            Published = fe.Published,
                            Content = HtmlToFb2(content, feedSetting.LoadImages, binaries)
                        };
                    })
                .GroupBy(e => e.FeedTitle)
                .ToList();

            callback(100, "Creating FB2 file...");

            logger.Info("Creating XML structure of the FB2 (intermediate format)");
            using (XmlWriter xw = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true }))
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
                    "Feeds: {0}; entries: {1}.".FormatWith(
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
                xw.WriteAttributeString("type", "text/css");
                Styles.ForEach(xw.WriteString);
                xw.WriteEndElement();

                xw.WriteStartElement("body");

                string formatString = Settings.Default.SurroundWithP ? "<p>{0}</p>" : "{0}";

                foreach (var group in entries)
                {
                    xw.WriteStartElement("section");

                    xw.WriteStartElement("title");
                    xw.WriteRaw(formatString.FormatWith(group.Key));
                    xw.WriteEndElement();

                    foreach (var feedEntry in group.OrderByDescending(fi => fi.Published))
                    {

                        xw.WriteStartElement("section");

                        xw.WriteStartElement("title");
                        xw.WriteRaw(formatString.FormatWith(feedEntry.Title));
                        xw.WriteEndElement();

                        xw.WriteElementString("subtitle", feedEntry.Published.ToString("dd MMMM yyyy (dddd) - HH:mm:ss"));
                        xw.WriteRaw(formatString.FormatWith(feedEntry.Content));
                        xw.WriteEndElement();
                    }

                    xw.WriteEndElement();
                }

                xw.WriteEndElement();


                xw.WriteStartElement("binary");
                xw.WriteAttributeString("id", "cover.png");
                xw.WriteAttributeString("content-type", "image/png");
                byte[] coverData = File.ReadAllBytes("Resources/cover.png");
                xw.WriteBase64(coverData, 0, coverData.Length);
                xw.WriteEndElement();

                foreach (var binary in binaries)
                {
                    xw.WriteStartElement("binary");
                    xw.WriteAttributeString("id", binary.Key);
                    xw.WriteAttributeString("content-type", "image/jpeg");
                    xw.WriteBase64(binary.Value, 0, binary.Value.Length);
                    xw.WriteEndElement();
                }

                xw.WriteEndDocument();
            }


            if (Settings.Default.MarkAsRead)
            {
                logger.Info("Marking downloaded and saved entries as read at Google Reader");
                callback(100, "Marking downloaded and saved entries as read...");

                GoogleReader.MarkAsRead(feedEntries);
                logger.Info("Marked all entries as read");
            }
        }
    }
}
