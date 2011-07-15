namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;

    using Majestic12;

    /// <summary>
    /// Methods working with HTML code or with classe from HtmlParser library.
    /// </summary>
    public static class HtmlUtils
    {
        /// <summary>
        /// Parses the HTML code into HtmlChunks.
        /// It uses deferred execution, so nothing is parsed actually when this method is called.
        /// </summary>
        public static IEnumerable<HTMLchunk> ParseHtml(string htmlCode)
        {
            if (htmlCode == null) throw new ArgumentNullException("htmlCode");

            // Note: couldn't find if any exception is thrown during HTML parsing
            using (var parser = new HTMLparser(htmlCode) { bDecodeEntities = true })
            {
                while (true)
                {
                    using (var chunk = parser.ParseNext())
                    {
                        // end of html reached
                        if (chunk == null) break;

                        yield return chunk.Clone();
                    }
                }
            }
        }

        /// <summary>
        /// Combines HtmlChunks to FB2 code.
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static string CombineToFb2(IEnumerable<HTMLchunk> chunks)
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
                            case "li":
                            case "dt":
                            case "dd":
                                goto case "br";
                        }

                        // now process tags that are not inserted when endClosure (e.g. <p/>, it can exist on badly formed pages)
                        if (!chunk.bEndClosure)
                        {
                            // formatting tags: strikethough, underlined, bold, italic
                            switch (chunk.oType)
                            {
                                case HTMLchunkType.OpenTag:
                                    switch (chunk.sTag)
                                    {
                                        case "strike":
                                        case "s":
                                            result.Append(@"<style name=""ss"">");
                                            break;
                                        case "u":
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
                                    }
                                    break;
                                case HTMLchunkType.CloseTag:
                                    switch (chunk.sTag)
                                    {
                                        case "strike":
                                        case "s":
                                        case "u":
                                        case "b":
                                        case "strong":
                                        case "i":
                                        case "em":
                                            result.Append(@"</style>");
                                            break;
                                    }
                                    break;
                            }

                            switch (chunk.sTag)
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
                                    result.AppendFormat("<{1}{0}>", chunk.sTag, slashIfClose);
                                    break;
                            }
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
        /// Returns chunks between the first open tag with the specified name and parameters and corresponding closing one.
        /// These open and close tags themselves are not included.<para/>
        /// Parameters are tuples where first element is parameter name and the second is its value.
        /// </summary>
        public static IEnumerable<HTMLchunk> TagContent(this IEnumerable<HTMLchunk> chunks, string tagName, params Tuple<string, string>[] parameters)
        {
            // skip until we find open tag with specified name and parameters
            chunks = chunks.SkipUntil(c => c.IsOpenTag(tagName, parameters));

            // skip one more item - it's the open tag itself
            chunks = chunks.Skip(1);

            // balance is difference between number of open and close tags with name == tagName
            // initial value is 1 because we've skipped first opening tag and will not count it
            int balance = 1;
            // now find the corresponding closing tag, returning everything before it
            foreach (var chunk in chunks)
            {
                // if tag has specified name, we have to update balance
                if (chunk.sTag == tagName)
                {
                    // update balance depending on the tag type
                    switch (chunk.oType)
                    {
                        case HTMLchunkType.OpenTag:
                            balance++;
                            break;
                        case HTMLchunkType.CloseTag:
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

        public static bool IsOpenTag(this HTMLchunk chunk, string tagName, params Tuple<string, string>[] parameters)
        {
            return
                chunk.oType == HTMLchunkType.OpenTag &&
                chunk.sTag == tagName &&
                parameters.All(par => chunk.oParams.ContainsKey(par.Item1)) &&
                parameters.All(par => chunk.oParams[par.Item1] == par.Item2);
        }
    }
}
