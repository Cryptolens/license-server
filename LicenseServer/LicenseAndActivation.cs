using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseServer
{
    public struct LAKey
    {
        public int ProductId { get; set; }
        public string Key { get; set; }
        public int SignMethod { get; set; }
    }

    public class LAResult
    {
        public SKM.V3.LicenseKey LicenseKey { get; set; }

        public string Response { get; set; }

        public DateTime SignDate { get; set; }
    }
}
