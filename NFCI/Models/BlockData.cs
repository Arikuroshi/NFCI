using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFCI.Models
{
    public sealed class BlockData
    {
        public int BlockNumber { get; set; }
        public byte[] Data { get; set; } = [];
    }
}
