using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFCI.Models
{
    public sealed class MemoryPage
    {
        public int Address { get; set; }
        public byte[] Data { get; set; } = [];
    }
}
