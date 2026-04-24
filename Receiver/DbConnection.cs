using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Receiver
{
    public class DbConnection
    {
        public static SqlConnection GetConnection()
        {
            return new SqlConnection("Server=DESKTOP-12R4TTE\\SQLEXPRESS;Database=HubspotDB;Trust Server Certificate=True;Trusted_Connection=True");
        }
    }
}
