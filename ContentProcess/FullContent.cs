﻿namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public abstract class FullContent
    {
        /// <summary>
        /// Instances of all implementations.
        /// </summary>
        private static readonly List<FullContent> Instances = new List<FullContent> 
        {
           new HabrContent(),
        };

        /// <summary>
        /// Checks whether an implementation exists which supports this url.
        /// </summary>
        public static bool Exists(string url)
        {
            if (url == null) return false;
            return Instances.Exists(i => i.IsMyUrl(url));
        }

        /// <summary>
        /// Gets the resulting article for this URL.<para/>
        /// This function performs all the work: find an implementation supporting this URL, process URL, download content and process the content.
        /// Null is returned when cannot load/process content, but URL is supported.
        /// </summary>
        public static string Get(string url)
        {
            if (url == null) throw new ArgumentNullException("url");

            FullContent instance = Instances.First(i => i.IsMyUrl(url));

            url = instance.ProcessUrl(url);
            try
            {
                string data = InternetUtils.Fetch(instance.ProcessUrl(url));

                data = instance.ProcessContent(data);
                return data;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks wherther specific implementation supports specific url.
        /// </summary>
        protected abstract bool IsMyUrl(string url);

        /// <summary>
        /// Changes the URL if needed, for example to download article from lightweight mobile site version.
        /// </summary>
        protected abstract string ProcessUrl(string url);

        /// <summary>
        /// Processes downloaded content of the article.
        /// </summary>
        protected abstract string ProcessContent(string content);
    }
}
