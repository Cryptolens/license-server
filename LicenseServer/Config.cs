using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseServer
{
    public class Config
    {
        public int Port { get; set; }
        public int CacheLength { get; set; }
        public bool OfflineMode { get; set; }
        public List<string> ActivationFiles { get; set; }
        public bool LocalFloatingServer { get; set; }

    }
}
