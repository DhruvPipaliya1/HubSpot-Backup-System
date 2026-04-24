using Microsoft.Data.SqlClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

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
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    string query = "INSERT INTO QueueTable(userId, directoryId, objectId, status, createdAt) VALUES (@userId, @directoryId, @objectId, @status, @createdAt)";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@directoryId", directoryId);
                        cmd.Parameters.AddWithValue("@objectId", objectId);
                        cmd.Parameters.AddWithValue("@status", 1);
                        cmd.Parameters.AddWithValue("@createdAt", DateTime.Now);

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
        /// Updates the status of a directory in the backup database identified by the specified directory ID.
        /// </summary>
        public static void UpdateDirectoryStatus(int dirId, int status)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    string query = "UPDATE FolderBackup SET directoryStatus = @directoryStatus WHERE id = @id";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id", dirId);
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
        public static string GetAccessToken(int user)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    string query = "SELECT accessToken FROM users WHERE id = @id";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id", user);

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
        public static (string nameKeyword, DateTime? dateFrom, DateTime? dateTo) GetSearchFilter(int userId, string objectType)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    string query = @"SELECT NameKeyword, DateFrom, DateTo 
                             FROM SearchFilter 
                             WHERE UserId = @UserId AND ObjectType = @ObjectType";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@ObjectType", objectType);

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
