namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using GoogleAPI.GoogleReader;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    [Serializable]
    public class FeedSettings
    {
        public string Id { get; set; }

        public bool Enabled { get; set; }

        public bool LoadImages { get; set; }

        public bool FullContent { get; set; }

        public bool LoadAllEntries { get; set; }

        public FeedSettings()
        {
            Enabled = true;
            LoadImages = true;
            FullContent = false;
            LoadAllEntries = false;
        }

        public FeedSettings(Feed feed)
            : this()
        {
            Id = feed.Id;
        }
    }
}
