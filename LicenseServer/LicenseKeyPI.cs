/**
 * Copyright (c) 2019 - 2021 Cryptolens AB
 * To use the license server, a separate subscription is needed. 
 * Pricing information can be found on the following page: https://cryptolens.io/products/license-server/
 * 
 * */

using SKM.V3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseServer
{
    /// <summary>
    /// A platform independent version of <see cref="LicenseKey"/> 
    /// that uses Unix Time (seconds) in all DateTime fields.
    /// </summary>
    [Serializable]
    public class LicenseKeyPI
    {
        public LicenseKeyPI()
        {
            Notes = "";
        }
        public int ProductId { get; set; }

        public int ID { get; set; }

        public string Key { get; set; }

        public long Created { get; set; }

        public long Expires { get; set; }

        public int Period { get; set; }

        public bool F1 { get; set; }
        public bool F2 { get; set; }
        public bool F3 { get; set; }
        public bool F4 { get; set; }
        public bool F5 { get; set; }
        public bool F6 { get; set; }
        public bool F7 { get; set; }
        public bool F8 { get; set; }

        public string Notes { get; set; }

        public bool Block { get; set; }

        public long GlobalId { get; set; }

        public CustomerPI Customer { get; set; }

        public List<ActivationDataPI> ActivatedMachines { get; set; }

        public bool TrialActivation { get; set; }

        public int MaxNoOfMachines { get; set; }

        public string AllowedMachines { get; set; }

        public List<DataObject> DataObjects { get; set; }

        public long SignDate { get; set; }

        public LicenseKey ToLicenseKey()
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            List<ActivationData> activationData = new List<ActivationData>();

            foreach (var item in ActivatedMachines)
            {
                activationData.Add(new ActivationData() { IP = item.IP, Mid = item.Mid, Time = epoch.AddSeconds(item.Time) });
            }

            return new LicenseKey
            {
                ProductId = ProductId,
                AllowedMachines = AllowedMachines,
                Block = Block,
                Created = epoch.AddSeconds(Created),
                DataObjects = DataObjects,
                F1 = F1,
                F2 = F2,
                F3 = F3,
                F4 = F4,
                F5 = F5,
                F6 = F6,
                F7 = F7,
                F8 = F8,
                GlobalId = GlobalId,
                ID = ID,
                Expires = epoch.AddSeconds(Expires),
                Key = Key,
                MaxNoOfMachines = MaxNoOfMachines,
                Notes = Notes,
                Period = Period,
                SignDate = epoch.AddSeconds(SignDate),
                Signature = "",
                TrialActivation = TrialActivation,
                Customer = Customer != null ? new Customer() { CompanyName = Customer.CompanyName, Created = epoch.AddSeconds(Customer.Created), Email = Customer.Email, Id = Customer.Id, Name = Customer.Name } : null,
                ActivatedMachines = activationData
            };
        }

    }

    public class CustomerPI
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string CompanyName { get; set; }

        public long Created { get; set; }
    }

    public class ActivationDataPI
    {
        public string Mid { get; set; }
        public string IP { get; set; }
        public long Time { get; set; }
        public string FriendlyName { get; set; }
    }

}
