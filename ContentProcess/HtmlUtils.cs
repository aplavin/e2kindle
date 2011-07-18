namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using HtmlParserMajestic.Wrappers;

    /// <summary>
    /// Methods working with HTML code or with classe from HtmlParser library.
    /// </summary>
    public static class HtmlUtils
    {
        /// <summary>
        /// Combines HtmlChunks to FB2 code.
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static string CombineToFb2(IEnumerable<HtmlChunk> chunks)
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
                        string slashIfClose = (chunk.TagType == HtmlTagType.Open ? string.Empty : "/");

                        // at first process tags that are inserted not depending on type (open/close/endClosure)
                        switch (chunk.TagName)
                        {
                            case "img":
                                // Note: add image to the code only if it has src and src is in format {digits}.jp[e]g
                                if (chunk.ParameterMatches("src", new Regex(@"^\d+.jpe?g$")))
                                    result.AppendFormat(@"<image l:href=""#{0}""/>", chunk.GetParameter("src"));
                                break;
                            case "hr":
                                result.Append("<subtitle>* * *</subtitle>");
                                break;
                            case "br":
                                result.Append("<empty-line/>");
                                break;
                        }

                        // formatting (strikethough, underlined, bold, italic) and lists (they are replaced by newlines)
                        if (chunk.TagType == HtmlTagType.Open)
                        {
                            switch (chunk.TagName)
                            {
                                case "strike":
                                case "s":
                                    result.Append(@"<style name=""s"">");
                                    break;
                                case "u":
                                case "a":
                                    result.Append(@"<style name=""u"">");
                                    break;
                                case "b":
                                case "strong":
                                    result.Append(@"<style name=""b"">");
                                    break;
                                case "i":
                                case "em":
                                    result.Append(@"<style name=""i"">");
                                    break;
                                case "li":
                                case "dt":
                                case "dd":
                                    result.Append("<empty-line/>");
                                    break;
                            }
                        }

                        if (chunk.TagType == HtmlTagType.Close &&
                            chunk.TagName.IsOneOf("strike", "s", "u", "a", "b", "strong", "i", "em"))
                        {
                            result.Append(@"</style>");
                        }

                        switch (chunk.TagName)
                        {
                            case "h1":
                            case "h2":
                            case "h3":
                            case "h4":
                            case "h5":
                            case "h6":
                                result.AppendFormat("<{0}subtitle>", slashIfClose);
                                break;
                            case "code":
                            case "pre":
                            case "tt":
                                result.AppendFormat("<{0}code>", slashIfClose);
                                break;
                            case "sup":
                            case "sub":
                            case "p":
                            case "cite":
                            case "table":
                            case "tr":
                            case "th":
                            case "td":
                                // append just tag, without its parameters
                                result.AppendFormat("<{1}{0}>", chunk.TagName, slashIfClose);
                                break;
                        }

                        break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Combines HTMLchunks to HTML code.
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static string CombineToHtml(this IEnumerable<HtmlChunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException("chunks");
            return chunks.Aggregate(chunk => chunk.Html);
        }

        /// <summary>
        /// Returns chunks between the first open tag with the specified name and parameters and corresponding closing one.
        /// These open and close tags themselves are not included.<para/>
        /// Parameters are tuples where first element is parameter name and the second is its value.
        /// </summary>
        public static IEnumerable<HtmlChunk> TagContent(this IEnumerable<HtmlChunk> chunks, string tagName, params Tuple<string, string>[] parameters)
        {
            // skip until we find open tag with specified name and parameters
            chunks = chunks.SkipUntil(c => IsOpenTag(c, tagName, parameters));

            // skip one more item - it's the open tag itself
            chunks = chunks.Skip(1);

            // balance is difference between number of open and close tags with name == tagName
            // initial value is 1 because we've skipped first opening tag and will not count it
            int balance = 1;
            // now find the corresponding closing tag, returning everything before it
            foreach (var chunk in chunks)
            {
                // if tag has specified name, we have to update balance
                if (chunk.Type == HtmlChunkType.Tag && chunk.TagName == tagName)
                {
                    // update balance depending on the tag type
                    switch (chunk.TagType)
                    {
                        case HtmlTagType.Open:
                            balance++;
                            break;
                        case HtmlTagType.Close:
                            balance--;
                            break;
                    }

                    // if balance is zero, then we've found the corresponding closing tag, so break
                    if (balance == 0) yield break;
                }

                // just return the chunk while we haven't found corresponding closing tag
                yield return chunk;
            }
        }

        public static bool IsOpenTag(this HtmlChunk chunk, string tagName, params Tuple<string, string>[] parameters)
        {
            return
                chunk.Type == HtmlChunkType.Tag &&
                chunk.TagType == HtmlTagType.Open &&
                chunk.TagName == tagName &&
                parameters.All(par => chunk.ParameterMatches(par.Item1, par.Item2));
        }
    }
}
