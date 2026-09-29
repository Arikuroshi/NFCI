using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFCI.Models
{
    public sealed record TagInfo(
        string ReaderName,
        string Uid,
        string? Ats,
        string? Atr);
}
