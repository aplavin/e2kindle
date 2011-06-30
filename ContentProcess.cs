using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

        /// <summary>
        /// Use multithread processing of feed items or not.
        /// </summary>
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
        static IEnumerable<KeyValuePair<string, string>> ProcessHtmlImages(ref string htmlContent)
        {
            // this regex matches all img src attributes. Yes, it's not good-looking =)
            var matches = Regex.Matches(htmlContent, @"(?<=img+.+src\=[\x27\x22])(?<Url>[^\x27\x22]*)(?=[\x27\x22])");
            var positions = (from Match match in matches
                             where !BadImageStrings.Any(bad => match.Value.Contains(bad))
                             select Utils.MakePair(match.Index, match.Length));

            var images = new List<KeyValuePair<string, string>>();
            if (positions.Empty()) return images;

            // start from the end 
            foreach (var position in positions.Reverse())
            {
                // get image url from html code
                string url = htmlContent.Substring(position.First, position.Second);

                // download the image
                byte[] image = Utils.RecodeImage(url);
                // if null (couldn't download) then skip this image
                if (image == null) continue;
                string imageBase64 = Convert.ToBase64String(image);

                // create unique name for image
                string imageName = String.Format("{0}.jpg", Interlocked.Increment(ref _uniqueImgNum));

                // add image to the output dictionary
                images.Add(new KeyValuePair<string, string>(imageName, imageBase64));

                // and change image name in the html code
                htmlContent = htmlContent.
                    Remove(position.First, position.Second).
                    Insert(position.First, imageName);
            }

            return images;
        }

        static readonly string[] HTMLEntities = { "&quot;", "&amp;", "&amp;", "&lt;", "&gt;", "&nbsp;", "&iexcl;", "&cent;", "&pound;", "&curren;", "&yen;", "&brvbar;", "&sect;", "&uml;", "&copy;", "&ordf;", "&laquo;", "&not;", "&shy;", "&reg;", "&macr;", "&deg;", "&plusmn;", "&sup2;", "&sup3;", "&acute;", "&micro;", "&para;", "&middot;", "&cedil;", "&sup1;", "&ordm;", "&raquo;", "&frac14;", "&frac12;", "&frac34;", "&iquest;", "&Agrave;", "&Aacute;", "&Acirc;", "&Atilde;", "&Auml;", "&Aring;", "&AElig;", "&Ccedil;", "&Egrave;", "&Eacute;", "&Ecirc;", "&Euml;", "&Igrave;", "&Iacute;", "&Icirc;", "&Iuml;", "&ETH;", "&Ntilde;", "&Ograve;", "&Oacute;", "&Ocirc;", "&Otilde;", "&Ouml;", "&times;", "&Oslash;", "&Ugrave;", "&Uacute;", "&Ucirc;", "&Uuml;", "&Yacute;", "&THORN;", "&szlig;", "&agrave;", "&aacute;", "&acirc;", "&atilde;", "&auml;", "&aring;", "&aelig;", "&ccedil;", "&egrave;", "&eacute;", "&ecirc;", "&euml;", "&igrave;", "&iacute;", "&icirc;", "&iuml;", "&eth;", "&ntilde;", "&ograve;", "&oacute;", "&ocirc;", "&otilde;", "&ouml;", "&divide;", "&oslash;", "&ugrave;", "&uacute;", "&ucirc;", "&uuml;", "&yacute;", "&thorn;", "&yuml;" };
        static readonly string[] XMLEntities = { "&#34;", "&#38;", "&#38;", "&#60;", "&#62;", "&#160;", "&#161;", "&#162;", "&#163;", "&#164;", "&#165;", "&#166;", "&#167;", "&#168;", "&#169;", "&#170;", "&#171;", "&#172;", "&#173;", "&#174;", "&#175;", "&#176;", "&#177;", "&#178;", "&#179;", "&#180;", "&#181;", "&#182;", "&#183;", "&#184;", "&#185;", "&#186;", "&#187;", "&#188;", "&#189;", "&#190;", "&#191;", "&#192;", "&#193;", "&#194;", "&#195;", "&#196;", "&#197;", "&#198;", "&#199;", "&#200;", "&#201;", "&#202;", "&#203;", "&#204;", "&#205;", "&#206;", "&#207;", "&#208;", "&#209;", "&#210;", "&#211;", "&#212;", "&#213;", "&#214;", "&#215;", "&#216;", "&#217;", "&#218;", "&#219;", "&#220;", "&#221;", "&#222;", "&#223;", "&#224;", "&#225;", "&#226;", "&#227;", "&#228;", "&#229;", "&#230;", "&#231;", "&#232;", "&#233;", "&#234;", "&#235;", "&#236;", "&#237;", "&#238;", "&#239;", "&#240;", "&#241;", "&#242;", "&#243;", "&#244;", "&#245;", "&#246;", "&#247;", "&#248;", "&#249;", "&#250;", "&#251;", "&#252;", "&#253;", "&#254;", "&#255;" };

        /// <summary>
        /// Converts HTML entities in content to XML ones.
        /// </summary>
        /// <param name="content">HTML code</param>
        /// <returns>The same code, but with converted entities</returns>
        static string EntitiesHtmlToXml(string content)
        {
            if (HTMLEntities.Length != XMLEntities.Length || HTMLEntities.Contains(null) || XMLEntities.Contains(null))
            {
                throw new InvalidDataException("Arrays of HTML and XML entities are wrong.");
            }

            for (int i = 0; i < HTMLEntities.Length; i++)
            {
                content = content.Replace(HTMLEntities[i], XMLEntities[i]);
            }
            return content;
        }

        /// <summary>
        /// Parses the HTML code into htmlchunks.
        /// </summary>
        /// <param name="htmlCode"></param>
        /// <returns></returns>
        public static List<HTMLchunk> ParseHTML(string htmlCode)
        {
            try
            {
                var chunks = new List<HTMLchunk>();
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
            catch (Exception ex)
            {
                logger.Error("Can't parse HTML code.", ex);
                return null;
            }
        }

        /// <summary>
        /// Convert HTMLchunks to FB2 code.
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        static string CombineToFb2(IEnumerable<HTMLchunk> chunks)
        {
            string result = string.Empty;

            foreach (var chunk in chunks)
            {
                switch (chunk.oType)
                {
                    case HTMLchunkType.Text:
                        string text = WebUtility.HtmlEncode(chunk.oHTML);
                        result += text;
                        // space is added due to a bug(?) in calibri with empty text between tags
                        if (!text.Contains(' '))
                            result += ' ';
                        break;
                    case HTMLchunkType.OpenTag:
                    case HTMLchunkType.CloseTag:
                        string slashIfClose = (chunk.oType == HTMLchunkType.CloseTag ? "/" : string.Empty);

                        // at 1st process tags that are inserted not depending on type (open/close/endClosure)
                        switch (chunk.sTag)
                        {
                            case "img":
                                // img tags are already processed, so they always have src param and it's correct
                                result += string.Format(@"<image l:href=""#{0}""/>", chunk.oParams["src"]);
                                break;
                            case "hr":
                                result += "<subtitle>* * *</subtitle>";
                                break;
                            case "br":
                                result += "<empty-line/>";
                                break;
                        }

                        // now process tags that are not inserted when endClosure (e.g. <p/>, it can exist on badly formed pages)
                        if (!chunk.bEndClosure)
                            switch (chunk.sTag)
                            {
                                case "strike":
                                case "s":
                                    result += string.Format("<{0}strikethrough>", slashIfClose);
                                    break;
                                case "u":
                                    // TODO: try to make correct formatting
                                    result += string.Format("<{0}emphasis>", slashIfClose);
                                    break;
                                case "b":
                                case "strong":
                                    result += string.Format("<{0}strong>", slashIfClose);
                                    break;
                                case "h1":
                                case "h2":
                                case "h3":
                                case "h4":
                                case "h5":
                                case "h6":
                                    result += string.Format("<{0}subtitle>", slashIfClose);
                                    break;
                                case "i":
                                case "em":
                                    result += string.Format("<{0}emphasis>", slashIfClose);
                                    break;
                                case "code":
                                case "pre":
                                case "tt":
                                    result += string.Format("<{0}code>", slashIfClose);
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
                                    result += string.Format("<{1}{0}>", chunk.sTag, slashIfClose);
                                    break;
                            }
                        break;
                }
            }

            return result;
        }

        public static string CombineToHtml(this IEnumerable<HTMLchunk> chunks)
        {
            string res = "";
            foreach (var chunk in chunks)
            {
                switch (chunk.oType)
                {
                    case HTMLchunkType.Text:
                        res += WebUtility.HtmlEncode(chunk.oHTML);
                        break;
                    case HTMLchunkType.OpenTag:
                        res += chunk.GenerateHTML();
                        break;
                    case HTMLchunkType.CloseTag:
                        res += chunk.GenerateHTML();
                        break;
                }
            }
            return res;
        }

        /// <summary>
        /// Convert HTML code to FB2, including images (they are downloaded, grayscaled and recompressed).
        /// </summary>
        /// <param name="content">Before: HTML code to be converted. After: converted FB2 code.</param>
        /// <param name="binaries">List of binary sections in the FB2 document. This function appends to it.</param>
        static void HtmlToFb2(ref string content, ref List<string> binaries)
        {
            // process images in html
            var images = ProcessHtmlImages(ref content);

            // and add them to binaries list as fb2 <binary> tags
            binaries.AddRange(
                images.Select(
                    p => @"<binary id=""{name}"" content-type=""image/jpeg"">{base64}</binary>".
                        Format(new { name = p.Key, base64 = p.Value })
                )
            );

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
        /// <param name="feedItems"></param>
        public static void CreateFb2(TextWriter writer, IEnumerable<IGrouping<GoogleFeed, GoogleFeedEntry>> feedEntries)
        {
            logger.Info("Processing {0} feeds entries", feedEntries.Sum(gr => gr.Count()));

            var binaries = new List<string>();
            feedEntries.AsParallel().
                ForAll(gr => gr.AsParallel().
                    ForAll(e =>
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
                foreach (string binary in binaries)
                {
                    xw.WriteRaw(binary);
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
