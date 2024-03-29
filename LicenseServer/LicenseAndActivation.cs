﻿/**
 * Copyright (c) 2019 - 2021 Cryptolens AB
 * To use the license server, a separate subscription is needed. 
 * Pricing information can be found on the following page: https://cryptolens.io/products/license-server/
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseServer
{
    public struct LAKey : LAKeyBase
    {
        public int ProductId { get; set; }
        public string Key { get; set; }
        public int SignMethod { get; set; }
    }

    public struct LAKeyGeneral : LAKeyBase
    {
        public int ProductId { get; set; }
        public string Key { get; set; }
    }

    public interface LAKeyBase
    {
        int ProductId { get; set; }
        string Key { get; set; }
    }

    public class LAResult
    {
        public SKM.V3.LicenseKey LicenseKey { get; set; }

        public string Response { get; set; }

        public DateTime SignDate { get; set; }
    }
}
