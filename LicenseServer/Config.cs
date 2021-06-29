using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

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

    [MessagePackObject]
    public class LicenseServerConfiguration
    {
        [Key(0)]
        public int Port { get; set; }
        [Key(1)]
        public int CacheLength { get; set; }
        [Key(2)]
        public bool OfflineMode { get; set; }
        [Key(3)]
        public List<string> ActivationFiles { get; set; }
        [Key(4)]
        public bool LocalFloatingServer { get; set; }
        [Key(5)]
        public string RSAPublicKey { get; set; }
        [Key(6)]
        public string ServerKey { get; set; }
        [Key(7)]
        public DateTimeOffset ValidUntil { get; set; }
    }

    [MessagePackObject]
    public class SerializedLSC
    {
        [Key(0)]
        public byte[] LSC { get; set; }
        [Key(1)]
        public byte[] Signature { get; set; }
    }
}