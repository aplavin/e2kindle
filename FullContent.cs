using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Majestic12;

namespace e2Kindle
{
    public class HabrContent : FullContent
    {
        private readonly Regex urlRegex =
            new Regex(@"^(https?://)?(www\.)?(m\.)?(habra)?habr.ru/((blogs/\w+)|(linker/go)|(company/\w+/blog)|(post))/(?<Num>\d+)/?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // TODO
        private const bool INCLUDE_COMMENTS = false;

        protected override bool IsMyUrl(string url)
        {
            if (url == null) throw new ArgumentNullException("url");
            return urlRegex.IsMatch(url);
        }

        protected override string ProcessUrl(string url)
        {
            if (url == null) throw new ArgumentNullException("url");
            return urlRegex.Replace(url, "http://m.habr.ru/post/${Num}/");
        }

        protected override string ProcessContent(string content)
        {
            if (content == null) throw new ArgumentNullException("content");

            var chunks = ContentProcess.ParseHTML(content);
            var articleChunks = chunks.
                 SkipUntil(c =>
                     c.oType == HTMLchunkType.OpenTag && c.sTag == "div" && c.oParams.ContainsKey("class") && c.oParams["class"] == "txt").
                     Skip(1);
            articleChunks = articleChunks.
                TakeUntil((c, i) =>
                    c.oType == HTMLchunkType.CloseTag && c.sTag == "div" &&
                    articleChunks.Take(i).Sum(ch =>
                        ch.sTag == "div" ?
                        ch.oType == HTMLchunkType.OpenTag ? 1 : -1 :
                        0) == 0);

            return articleChunks.CombineToHtml();
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

    public abstract class FullContent
    {
        protected abstract bool IsMyUrl(string url);

        protected abstract string ProcessUrl(string url);

        protected abstract string ProcessContent(string content);

        public static bool Exists(string url)
        {
            if (url == null) throw new ArgumentNullException("url");
            return Instances.Count(i => i.IsMyUrl(url)) == 1;
        }

        public static string Get(string url)
        {
            if (url == null) throw new ArgumentNullException("url");

            FullContent instance = Instances.Single(i => i.IsMyUrl(url));

            url = instance.ProcessUrl(url);
            string data;
            try
            {
                data = Utils.Download(url);
            }
            catch
            {
                return null;
            }
            data = instance.ProcessContent(data);
            return data;
        }

        private static readonly List<FullContent> Instances = 
            new List<FullContent>{
                new HabrContent(),
            };
    }
}
