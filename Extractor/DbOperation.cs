using Microsoft.Data.SqlClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace Extractor
{
    public class DbOperation
    {
        /// <summary>
        /// Inserts a new entry into the queue table for the specified user, directory, and object.
        /// </summary>
        public static void InsertObjectInDb(int userId, int directoryId, string objectId)
        {
            try
            {
                string json = GetUserDataSet(userId);

                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@IsInsertObjectInDb", 1);
                        cmd.Parameters.AddWithValue("@QuserId", userId);
                        cmd.Parameters.AddWithValue("@QdirectoryId", directoryId);
                        cmd.Parameters.AddWithValue("@QobjectId", objectId);
                        cmd.Parameters.AddWithValue("@Status", 1);
                        cmd.Parameters.AddWithValue("@QcreatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@QDataSet", json);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in InsertObjectInDb" + ex.Message);
            }
        }


        /// <summary>
        /// Retrieves the user data set as a string for the specified user identifier by executing a stored procedure.
        /// </summary>
        public static string GetUserDataSet(int userId)
        {
            using (SqlConnection con = DbConnection.GetConnection())
            {
                con.Open();

                using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IsGetUserDataSet", 1);
                    cmd.Parameters.AddWithValue("@UuserId", userId);

                    return cmd.ExecuteScalar()?.ToString();
                }
            }
        }


        /// <summary>
        /// Updates the status of a directory in the backup database identified by the specified directory ID.
        /// </summary>
        public static void UpdateDirectoryStatus(int dirId, int status)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsUpdateDirectoryStatus", 1);
                        cmd.Parameters.AddWithValue("@FolderId", dirId);
                        cmd.Parameters.AddWithValue("@directoryStatus", status);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in UpdateDirectoryStatus" + ex.Message);
            }
        }


        /// <summary>
        /// Retrieves the access token associated with the specified user identifier.
        /// </summary>
        public static string GetAccessToken()
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetAccessToken", 1);
                        cmd.Parameters.AddWithValue("@UuserId", 1);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return reader.GetString(0);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetAccessToken: " + ex.Message);
            }

            return null;
        }


        /// <summary>
        /// Retrieves the saved search filter for the specified user and object type.
        /// </summary>
        public static (string nameKeyword, DateTime? dateFrom, DateTime? dateTo) GetSearchFilter()
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@IsGetSearchFilter", 1);
                        cmd.Parameters.AddWithValue("@UserId", 1);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string nameKeyword = reader.IsDBNull(0) ? null : reader.GetString(0);
                                DateTime? dateFrom = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                                DateTime? dateTo = reader.IsDBNull(2) ? null : reader.GetDateTime(2);

                                return (nameKeyword, dateFrom, dateTo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetSearchFilter: " + ex.Message);
            }

            return (null, null, null);
        }
    }
}
