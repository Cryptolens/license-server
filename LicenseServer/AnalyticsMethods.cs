using SKM.V3.Methods;
using SKM.V3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseServer
{
    public class AnalyticsMethods
    {
        public const int maxFloatingInterval = 10000;

        public static string SummaryAuditFloating(int productId, string licenseKey, string token, int maxNoOfMachines)
        {
            // This token should have GetKey and GetWebAPILog permission.
            
            // We need to set 3 things: the product (5693), the license, the token and the time window (e.g., 24 hours back in time)
            var res = GetAuditLog(productId, licenseKey, token, 24, maxNoOfMachines);

            var sb = new StringBuilder();
            
            sb.Append("Max number of machines: " + res.MaxNoOfMachines);
            sb.Append("<br>");
            sb.Append($"At this point, {res.MaxNoOfMachines - res.UsedFloatingLicenses} seats are available.");
            sb.Append("<br>");
            sb.Append("Displaying history of events 24 h back in time.");
            sb.Append("<br>");

            foreach (var log in res.Logs)
            {
                sb.Append(log.Time + "\t" + log.Action + "\t" + log.FreeSeatsRemaining + "\t" + log.MachineId);
                sb.Append("<br>");
            }

            return sb.ToString();
        }

        public static AuditLog GetAuditLog(int productId, string licenseKey, string token, int hours, int maxNoOfMachines)
        {
            var auditLog = new AuditLog();

            //// 1. Getting the LicenseKey (for max no of machines)
            //var res = Key.GetKey(token, new KeyInfoModel { ProductId = productId, Key = licenseKey, Metadata = true });
            auditLog.MaxNoOfMachines = maxNoOfMachines; //res.LicenseKey.MaxNoOfMachines;

            // 2. Retrieving the logs associated with the license key. 

            var rawLogs = new List<WebAPILog>();
            long pointer = 0;

            DateTimeOffset period = DateTimeOffset.UtcNow.AddHours(-hours);
            while (true)
            {
                var res2 = AI.GetWebAPILog(token, new GetWebAPILogModel
                {
                    Limit = 1000,
                    ProductId = productId,
                    Key = licenseKey,
                    EndingBefore = (int)pointer,
                    OrderBy = "Time descending",
                    States = new List<short> { 2014, 2015, 6011 }
                });

                if (res2.Logs.Count == 0 || res2.Logs.First().Time < period.AddSeconds(-maxFloatingInterval).ToUnixTimeSeconds())
                {
                    break;
                }

                rawLogs.AddRange(res2.Logs);
                pointer = res2.Logs.Last().Id;
            }

            rawLogs = rawLogs.Where(x => x.FloatingExpires >= period.ToUnixTimeSeconds() ||
                                         x.Time >= period.ToUnixTimeSeconds() ||
                                         (x.Time < period.ToUnixTimeSeconds() && x.State == 6011)
            ).OrderBy(x => x.Time).ToList();



            var preprocesedLogs = new List<Activity>();

            var floatingLicenses = new Dictionary<string, long>();

            foreach (var webAPIEntry in rawLogs)
            {
                // we don't want to include activations/deactivations that were not successful.
                if (webAPIEntry.State % 100 / 10 == 2) { continue; }

                // ignore the node-locked case
                if (webAPIEntry.State == 2010 || webAPIEntry.State == 2011 || webAPIEntry.State == 2012 || webAPIEntry.State == 2013) { continue; }

                string action = "";
                if (webAPIEntry.State / 1000 == 2)
                {
                    action = "Activation";

                    if (webAPIEntry.State == 2014 || webAPIEntry.State == 2015)
                    {
                        // floating
                        if (floatingLicenses.ContainsKey(webAPIEntry.MachineCode))
                        {
                            floatingLicenses[webAPIEntry.MachineCode] = webAPIEntry.FloatingExpires;
                            action = "Verification";
                        }
                        else
                        {
                            floatingLicenses.Add(webAPIEntry.MachineCode, webAPIEntry.FloatingExpires);
                        }
                    }
                }
                else if (webAPIEntry.State / 1000 == 6)
                {
                    action = "Deactivation";

                    if (webAPIEntry.State == 6011)
                    {
                        //release of floating
                        if (floatingLicenses.ContainsKey(webAPIEntry.MachineCode))
                        {
                            floatingLicenses.Remove(webAPIEntry.MachineCode);
                            //floatingLicenses[webAPIEntry.MachineCode] = 0;
                        }
                    }
                }
                else
                {
                    // we are not interested in other events.
                    continue;
                }

                int activeFloatingSeats = floatingLicenses.Where(x => x.Value > webAPIEntry.Time).Count();

                preprocesedLogs.Add(new Activity { Time = DateTimeOffset.FromUnixTimeSeconds(webAPIEntry.Time).DateTime, Action = action, MachineId = webAPIEntry.MachineCode, FreeSeatsRemaining = auditLog.MaxNoOfMachines - activeFloatingSeats });

                // now, let's check if some devices are no longer used.
                var inactiveFloatingSeats = floatingLicenses.Where(x => x.Value <= webAPIEntry.Time && x.Value > 0);

                foreach (var inactiveDevice in inactiveFloatingSeats)
                {
                    preprocesedLogs.Add(new Activity { Time = DateTimeOffset.FromUnixTimeSeconds(inactiveDevice.Value).DateTime, Action = "Deactivation", MachineId = inactiveDevice.Key, FreeSeatsRemaining = int.MinValue/*auditLog.MaxNoOfMachines - activeFloatingSeats*/ });
                    floatingLicenses.Remove(inactiveDevice.Key);
                }
            }

            var timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // one final time, let's check if some devices are no longer used.
            var inactiveFloatingSeatsFinal = floatingLicenses.Where(x => x.Value <= timeNow && x.Value > 0);

            foreach (var inactiveDevice in inactiveFloatingSeatsFinal)
            {
                preprocesedLogs.Add(new Activity { Time = DateTimeOffset.FromUnixTimeSeconds(inactiveDevice.Value).DateTime, Action = "Deactivation", MachineId = inactiveDevice.Key, FreeSeatsRemaining = int.MinValue/*auditLog.MaxNoOfMachines - activeFloatingSeats*/ });
                floatingLicenses.Remove(inactiveDevice.Key);
            }
            preprocesedLogs = preprocesedLogs.OrderBy(x => x.Time).ToList();

            // fix the number of active seats for licenses that were automatically deactivated.
            for (int i = 0; i < preprocesedLogs.Count; i++)
            {
                if (preprocesedLogs[i].FreeSeatsRemaining == int.MinValue)
                {
                    // do Min just in case floating overdraft was used.
                    preprocesedLogs[i].FreeSeatsRemaining = Math.Min(preprocesedLogs[i - 1].FreeSeatsRemaining + 1, auditLog.MaxNoOfMachines);
                }
            }

            auditLog.Logs = preprocesedLogs;
            auditLog.UsedFloatingLicenses = floatingLicenses.Where(x => x.Value > timeNow).Count();

            return auditLog;
        }

    }

    public class AuditLog
    {
        public int MaxNoOfMachines { get; set; }
        public List<Activity> Logs { get; set; }
        //public int NodeLockedDevices { get; set; }
        public int UsedFloatingLicenses { get; set; }

    }

    public class Activity
    {
        public DateTime Time { get; set; }
        public string Action { get; set; }
        public string MachineId { get; set; }

        public int FreeSeatsRemaining { get; set; }
    }

}
