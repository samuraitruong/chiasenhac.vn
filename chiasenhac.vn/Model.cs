using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ChiaSeNhac.VN
{
    public class PageWithItems {
    public string Name { get; set; }
    public ConcurrentBag<PageItem> Pages { get; set; }
    public string Url { get; set; }

        public PageWithItems()
    {
        Pages = new ConcurrentBag<PageItem>();
    }
}

    public class Artist : PageWithItems
    {
        public string Id { get; set; }

    }
    public class Album : PageWithItems
    {

    }
    public class PageItem
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Label { get; set; }
    }
    public partial class FileItem
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("default")]
        public bool Default { get; set; }
        public string Title { get; internal set; }
    }

    public class PlayerConfig
    {
        [JsonProperty("sources")]
        public ConcurrentBag<FileItem> Sources { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }

        public FileItem LosslessFile
        {
            get
            {
                if (this.Sources == null) return null;
                var item = this.Sources.FirstOrDefault(x => x.Label == "Lossless");
                if (item == null) return null;
                item.Title = this.Title;
                return item;
            }
        }
    }
}
