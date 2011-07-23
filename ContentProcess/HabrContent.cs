namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using HtmlParserMajestic.Wrappers;

    public class HabrContent : FullContent
    {
        private const bool ADD_COMMENTS = true;

        private static readonly Regex UrlRegex = new Regex(
            @"^(https?://)?(www\.)?(m\.)?(habra)?habr.ru/((blogs/\w+)|(linker/go)|(company/\w+/blog)|(post))/(?<Num>\d+)/?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CodeRegex = new Regex(@"<code.*>(.*)</code>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override bool IsMyUrl(string url)
        {
            return url != null && UrlRegex.IsMatch(url);
        }

        protected override string ProcessUrl(string url)
        {
            if (url == null) throw new ArgumentNullException("url");
            return UrlRegex.Replace(url, "http://m.habr.ru/post/${Num}/");
        }

        protected override string ProcessContent(string content)
        {
            if (content == null) throw new ArgumentNullException("content");

            // in <code>*</code> replace newline characters with <br> tag
            // it's easier to perform this before parsing
            /*content = CodeRegex.Replace(content,
                match =>
                {
                    string codeContent = match.Groups[1].Value;
                    if(codeContent.Count())
                    return "<code>" + codeContent.Replace("\r\n", "\n").Replace("\n", "<br/>") + "</code>";
                });*/

            var pageChunks = HtmlParser.Parse(content).ToList();

            var resultChunks = Enumerable.Empty<HtmlChunk>();

            // article is in <div class="txt"> tag
            var articleChunks = pageChunks.TagContent("div", Tuple.Create("class", "txt"));
            resultChunks = resultChunks.Concat(articleChunks);


            if (ADD_COMMENTS)
            {
                // comments are in <div class="cmts"> tag
                var commentsChunks = pageChunks.TagContent("div", Tuple.Create("class", "cmts")).
                    ToList();

                for (int i = 0; i < commentsChunks.Count; i++)
                {
                    if (i + 3 < commentsChunks.Count &&
                        commentsChunks[i].IsOpenTag("div", Tuple.Create("class", "m")))
                    {
                        commentsChunks[i] = new HtmlChunk("i", HtmlTagType.Open); // instead of <div class=m>
                        commentsChunks[i + 2] = new HtmlChunk("i", HtmlTagType.Close); // instead of </div>

                        commentsChunks[i + 1] = new HtmlChunk("-&gt; " + commentsChunks[i + 1].Text.GetBefore(",")); // from "{nickname}, {time}" to "-> {nickname}"
                        commentsChunks.Insert(i + 2, new HtmlChunk("br", HtmlTagType.SelfClose)); // add line break between nickname and comment

                        commentsChunks.Insert(i, new HtmlChunk("br", HtmlTagType.SelfClose)); // add line break before comment (before author nickname)
                    }
                }

                resultChunks = resultChunks.
                    Concat(new HtmlChunk("hr", HtmlTagType.SelfClose)). // add separator between article and comments
                    Concat(commentsChunks); // and comments themselves
            }

            return resultChunks.CombineToHtml();
        }

        public static bool Test()
        {
            var hc = new HabrContent();
            const string res = "http://m.habr.ru/post/123456/";
            return
               hc.IsMyUrl("http://habrahabr.ru/blogs/abcdefgh/123456") &&
               hc.IsMyUrl("http://habrahabr.ru/blogs/_abcd_efgh_/123456") &&
               hc.IsMyUrl("https://www.habrahabr.ru/blogs/abcdefgh/123456") &&
               hc.IsMyUrl("http://m.habrahabr.ru/blogs/abcdefgh/123456") &&
               hc.IsMyUrl("http://habrahabr.ru/blogs/abcdefgh/123456/") &&
               hc.IsMyUrl("http://habrahabr.ru/linker/go/123456/") &&

               res == hc.ProcessUrl("http://habrahabr.ru/blogs/abcdefgh/123456") &&
               res == hc.ProcessUrl("http://habrahabr.ru/blogs/_abcd_efgh_/123456") &&
               res == hc.ProcessUrl("https://www.habrahabr.ru/blogs/abcdefgh/123456") &&
               res == hc.ProcessUrl("http://m.habrahabr.ru/blogs/abcdefgh/123456") &&
               res == hc.ProcessUrl("http://habrahabr.ru/blogs/abcdefgh/123456/") &&
               res == hc.ProcessUrl("http://habrahabr.ru/linker/go/123456/");
        }
    }
}