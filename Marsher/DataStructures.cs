using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Marsher
{
    [Table("items")]
    public class QaItem
    {
        [Column("service", Order = 1)]
        [Required]
        public QaService Service { get; set; }
        [Key]
        [Required]
        [Column("id", Order = 0)]
        public string Id { get; set; }
        [Column("content")]
        [Required]
        public string Content { get; set; }

        [Column("translation")]
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
