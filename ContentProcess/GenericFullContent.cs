namespace e2Kindle.ContentProcess
{
    using HtmlParserMajestic.Wrappers;
    using NReadability;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class GenericFullContent
    {
        private static readonly NReadabilityWebTranscoder Transcoder = new NReadabilityWebTranscoder();

        public static string Get(string url)
        {
            bool isExtracted;
            string result = Transcoder.Transcode(url, new DomSerializationParams { PrettyPrint = true }, out isExtracted);
            if (!isExtracted) return null;

            return HtmlParser.Parse(result)
                .TagContent("div", 3)
                .Combine();
        }
    }
}
