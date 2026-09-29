using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFCI.Models
{
    public sealed class CardDump
    {
        public string ReaderName { get; set; } = "";
        public string Uid { get; set; } = "";
        public string TagType { get; set; } = "";
        public string Atr { get; set; } = "";
        public string Ats { get; set; } = "";
        public string Atqa { get; set; } = "";
        public byte Sak { get; set; }
        public DateTime ScannedAt { get; set; }
        public List<MemoryPage> Memory { get; set; } = [];
    }
}
