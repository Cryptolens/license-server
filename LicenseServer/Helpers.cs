using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data.SQLite;
using System.Data.Linq;

namespace LicenseServer
{
    public class Helpers
    {
        public static void InitDB()
        {
            using (var conn = new SQLiteConnection($"URI=file:{Environment.CurrentDirectory}\\data.sqlite"))
            {
                conn.Open();

                using (var transaction = conn.BeginTransaction())
                {
                    var cmd = new SQLiteCommand(conn);
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS LicenseUser(productid INT, licensekey TEXT, machinecode TEXT, username TEXT, lastaccess DATETIME, PRIMARY KEY (productid, licensekey,machinecode));";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS UserLog(id INT, productid INT, licensekey TEXT, machinecode TEXT, username TEXT, time DATETIME, PRIMARY KEY (id));";
                    cmd.ExecuteNonQuery();
                    transaction.Commit();
                }

                conn.Close();
            }
        }

        public static void UpdateUser(int productId, string licenseKey, string machineCode, string username)
        {
            using (var conn = new SQLiteConnection($"URI=file:{Environment.CurrentDirectory}\\data.sqlite"))
            {
                conn.Open();

                using (var transaction = conn.BeginTransaction())
                {
                    var cmd = new SQLiteCommand(conn);

                    cmd.CommandText = @"INSERT OR REPLACE INTO licenseuser (productid, licensekey,machinecode,username,lastaccess) VALUES (@product, @license,@machine,@user,@time)";
                    cmd.Parameters.AddWithValue("@product", productId);
                    cmd.Parameters.AddWithValue("@license", licenseKey);
                    cmd.Parameters.AddWithValue("@machine", machineCode);
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@time", DateTime.UtcNow);
                    cmd.ExecuteNonQuery();
                    transaction.Commit();
                }

                conn.Close();
            }
        }

        //public GetParameters

    }
}
