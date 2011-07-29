namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using HtmlParserMajestic.Wrappers;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class Fb2Utils
    {
        /// <summary>
        /// Combines HtmlChunks to FB2 code.
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static string Combine(IEnumerable<HtmlChunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException("chunks");

            var result = new StringBuilder();

            foreach (var chunk in chunks)
            {
                switch (chunk.Type)
                {
                    case HtmlChunkType.Text:
                        string text = WebUtility.HtmlEncode(chunk.Text);
                        result.Append(text);
                        break;
                    case HtmlChunkType.Tag:
                        AppendConvertedChunk(result, chunk);
                        break;
                }
            }

            return result.ToString();
        }

        private static void AppendConvertedChunk(StringBuilder sb, HtmlChunk chunk)
        {
            switch (chunk.TagType)
            {
                case HtmlTagType.Open:
                    switch (chunk.TagName)
                    {
                        case "strike":
                        case "s":
                            AppendStyle(sb, "s");
                            break;
                        case "u":
                            AppendStyle(sb, "u");
                            break;
                        case "b":
                        case "strong":
                            AppendStyle(sb, "b");
                            break;
                        case "i":
                        case "em":
                            AppendStyle(sb, "i");
                            break;
                        case "big":
                            AppendStyle(sb, "large");
                            break;

                        case "font":
                            int size;
                            if (chunk.HasParameter("color"))
                            {
                                AppendColorStyle(sb, chunk.GetParameter("color"));
                            }
                            else if (chunk.HasParameter("size") && Int32.TryParse(chunk.GetParameter("size"), out size))
                            {
                                if (size < 3)
                                    AppendStyle(sb, "small");
                                else if (size >= 7)
                                    AppendStyle(sb, "xxlarge");
                                else if (size >= 5)
                                    AppendStyle(sb, "xlarge");
                                else if (size > 3)
                                    AppendStyle(sb, "large");
                            }
                            else
                            {
                                sb.Append("<style>"); // because we close style then anyway
                            }
                            break;

                        case "a":
                            AppendStyle(sb, "u");
                            AppendColorStyle(sb, "#222222");
                            break;

                        case "li":
                        case "dt":
                        case "dd":
                            sb.Append("<empty-line/>");
                            break;

                        case "h1":
                            AppendStyle(sb, "xxlarge");
                            goto case "hALL";
                        case "h2":
                        case "h3":
                            AppendStyle(sb, "xlarge");
                            goto case "hALL";
                        case "h4":
                        case "h5":
                        case "h6":
                            AppendStyle(sb, "large");
                            goto case "hALL";
                        case "hALL":
                            AppendStyle(sb, "b");
                            break;

                        case "code":
                        case "pre":
                        case "tt":
                            sb.Append("<code>");
                            AppendStyle(sb, "mono");
                            break;

                        case "sup":
                        case "sub":
                        case "p":
                        case "cite":
                        case "table":
                        case "tr":
                        case "th":
                        case "td":
                            sb.AppendFormat("<{0}>", chunk.TagName);
                            break;
                    }
                    goto default;
                case HtmlTagType.Close:
                    switch (chunk.TagName)
                    {
                        case "strike":
                        case "s":
                        case "u":
                        case "b":
                        case "strong":
                        case "i":
                        case "em":
                        case "big":
                            AppendCloseStyle(sb);
                            break;

                        case "font":
                            AppendCloseStyle(sb);
                            break;

                        case "a":
                            AppendCloseStyle(sb);
                            AppendCloseStyle(sb);
                            break;

                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                            AppendCloseStyle(sb);
                            AppendCloseStyle(sb);
                            break;

                        case "code":
                        case "pre":
                        case "tt":
                            AppendCloseStyle(sb);
                            sb.Append("</code>");
                            break;

                        case "sup":
                        case "sub":
                        case "p":
                        case "cite":
                        case "table":
                        case "tr":
                        case "th":
                        case "td":
                            sb.AppendFormat("</{0}>", chunk.TagName);
                            break;
                    }
                    goto default;
                case HtmlTagType.SelfClose:
                    goto default;
                default:
                    switch (chunk.TagName)
                    {
                        case "img":
                            // Note: add image only if it has src and src is in format {digits}.jp[e]g
                            if (chunk.ParameterMatches("src", new Regex(@"^\d+.jpe?g$")))
                            {
                                sb.AppendFormat(@"<image l:href=""#{0}""/>", chunk.GetParameter("src"));
                            }
                            break;
                        case "hr":
                            sb.Append("<subtitle>* * *</subtitle>");
                            break;
                        case "br":
                        case "ol":
                        case "ul":
                            sb.Append("<empty-line/>");
                            break;
                    }
                    break;
            }
        }

        private static void AppendStyle(StringBuilder sb, string styleName)
        {
            sb.AppendFormat("<style name=\"{0}\">", styleName);
        }

        private static void AppendCloseStyle(StringBuilder sb)
        {
            sb.Append("</style>");
        }

        private static char HtmlColorToGray(string htmlColor)
        {
            try
            {
                var color = ColorTranslator.FromHtml(htmlColor);
                return ((int)((color.R * 0.3) + (color.G * 0.59) + (color.B * 0.11)) / 16).ToString("X")[0];
            }
            catch
            {
                return '0';
            }
        }

        private static void AppendColorStyle(StringBuilder sb, string htmlColor)
        {
            AppendStyle(sb, "col{0}".FormatWith(HtmlColorToGray(htmlColor)));
        }
    }
}