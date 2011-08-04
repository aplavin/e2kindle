namespace e2Kindle.ContentProcess
{
    using HtmlParserMajestic.Wrappers;
    using NReadability;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class GenericFullContent
    {
        // TODO: fix it for multithreading
        private static readonly NReadabilityWebTranscoder Transcoder = new NReadabilityWebTranscoder();

        public static string Get(string url)
        {
            bool isExtracted;
            string result = new NReadabilityWebTranscoder().Transcode(url, new DomSerializationParams { PrettyPrint = true }, out isExtracted);
            if (!isExtracted) return null;

            return HtmlParser.Parse(result)
                .TagContent("div", 3)
                .Combine();
        }
    }
}
