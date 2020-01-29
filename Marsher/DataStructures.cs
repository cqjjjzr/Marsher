using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Marsher
{
    [Table("items")]
    public class QaItem
    {
        [Column("service", Order = 1)]
        [JsonProperty("service")]
        [Required]
        public QaService Service { get; set; }
        [Key]
        [Required]
        [Column("id", Order = 0)]
        [JsonProperty("id")]
        public string Id { get; set; }
        [Column("content")]
        [JsonProperty("content")]
        [Required]
        public string Content { get; set; }

        [Column("translation")]
        [JsonProperty("translation")]
        public string Translation { get; set; }
    }

    public enum QaService
    {
        Marshmallow = 0x10, Peing = 0x20
    }

    public class QaList
    {
        public string Name { get; set; }
        public List<QaItem> Items { get; } = new List<QaItem>();
    }

    public class QaListStubs
    {
        public string Name { get; set; }
        public List<string> Items { get; } = new List<string>();

        public string Filename { get; set; }

        public bool Locked { get; set; }
    }
}
