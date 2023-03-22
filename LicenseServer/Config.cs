/**
 * Copyright (c) 2019 - 2023 Cryptolens AB
 * To use the license server, a separate subscription is needed. 
 * Pricing information can be found on the following page: https://cryptolens.io/products/license-server/
 * 
 * */

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
        [Key(8)]
        public string PathToCacheFolder { get; set; }
        [Key(9)]
        public string WebAPILogAccessToken { get; set; }
        [Key(10)]
        public string PathToConfigFile { get; set; }

    }

    [MessagePackObject]
    public class SerializedLSC
    {
        [Key(0)]
        public byte[] LSC { get; set; }
        [Key(1)]
        public byte[] Signature { get; set; }
    }

    public class Constants
    {
        public static string RSAPubKey = "<RSAKeyValue><Modulus>0l10Tgew1flcPcjt6R8ciWSRRG80zAdfz2+brNbYTxocqFYrQEELx9H0WWcyF9dh0M6OuY5nwjUh7dmJUcJyP56NMd8+1ozj7yUMckltJbRVwJVvVVhUoDRIn7jIfQuJKdXWpvXxCXlw9/WkoJMVYvKAGMvoBIb5BLbB3KT0DxjS3TXmymMTZYLWyBF4VxD53JQezX6r4wBEK9HFGw5aos3J585VeM3/SnUL6RL8MvmrOp/W/4iJJ5TvS/XXz/Kdr6Nmgm3jn6YO+IJ9mkdfd/nuqh8MTeP/Z8+nxEfI0rGx6LQyjwkr1Dyx6ILtns+YVpfG8af1EcPVXvxF+eLYhQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}