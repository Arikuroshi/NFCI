using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace NFCI.Logging
{
    public static class ApduLogger
    {
        public static void LogRead(
            string reader,
            string commandHex,
            string responseHex,
            int length,
            string atrHex)
        {
            Log.Information(
                $"CardRead {reader} Command:{commandHex} Response:{responseHex} Len:{length} ATR:{atrHex}",
                reader,
                commandHex,
                responseHex,
                length,
                atrHex);
        }
    }
}
