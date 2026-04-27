using HubSpotBackupSystem.Models;
using Microsoft.Data.SqlClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace HubSpotBackupSystem
{
    public class DbOperation
    {
        /// <summary>
        /// Retrieves the identifiers of all users with an active watch status.
        /// </summary>
        public static List<int> GetUsers()
        {
            var users = new List<int>();

            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetUsers", 1);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                users.Add(id);
                            }
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetUser: " + ex.Message);
            }

            return users;
        }


        /// <summary>
        /// Retrieves a list of object types associated with the specified user from the database.
        /// </summary>
        public static List<string> GetObjectFromDB(int user)
        {
            List<string> result = new List<string>();

            try
            {
                using (SqlConnection conn = DbConnection.GetConnection())
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetObjectTypeByDirectoryId", 1);
                        cmd.Parameters.AddWithValue("@FolderId", user);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(reader.GetString(0));
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetObjectFromDB: " + ex.Message);
            }

            return result;
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
                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetAccessToken", 1);
                        cmd.Parameters.AddWithValue("@UuserId", user);

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
        /// Inserts a new directory record for the specified user and object type into the FolderBackup table if it does
        /// not already exist.
        /// </summary>
        public static void InsertDirectory(int userId, string objectType)
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

                        cmd.Parameters.AddWithValue("@IsInsertDirectory", 1);
                        cmd.Parameters.AddWithValue("@UuserId", userId);
                        cmd.Parameters.AddWithValue("@DobjectType", objectType);
                        cmd.Parameters.AddWithValue("@FolderDataSet", json);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in InsertDirectory: " + ex.Message);
            }
        }


        /// <summary>
        /// Retrieves the data set associated with the specified user identifier from the database.
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
        /// Retrieves a list of pending directory extraction requests for the specified user.
        /// </summary>
        public static List<ExtractorMessage> GetPendingDirectories(int userId)
        {
            
            var directories = new List<ExtractorMessage>();

            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();


                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetPendingDirectories", 1);
                        cmd.Parameters.AddWithValue("@UuserId", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var dir = new ExtractorMessage
                                {
                                    Id = reader.GetInt32(0),
                                    userId = reader.GetInt32(1),
                                    ObjectType = reader.GetString(2)
                                };

                                directories.Add(dir);
                            }
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetPendingDirectories: ", ex.Message);
            }

            return directories;
        }



        
        /// <summary>
        /// Resets the directory status for all folder backups associated with the specified user.
        /// </summary>
        public static void ResetDirectoryStatusByUserId(int userId, int status)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsResetDirectoryStatusByUserId", 1);
                        cmd.Parameters.AddWithValue("@directoryStatus", status);
                        cmd.Parameters.AddWithValue("@DuserId", userId);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in ResetDirectoryStatusByUserId: " + ex.Message);
            }
        }


        /// <summary>
        /// Updates the watch status for the specified user in the database.
        /// </summary>
        public static void UpdateWatchStatusForUser(int userId, int status)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsUpdateWatchStatusForUser", 1);
                        cmd.Parameters.AddWithValue("@watchStatus", status);
                        cmd.Parameters.AddWithValue("@uuserId", userId);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in UpdateWatchStatusForUser: " + ex.Message);
            }
        }


        /// <summary>
        /// Resets the watch status for all users by setting the watchStatus field to its default value.
        /// </summary>
        public static void ResetWatchStatusForUser()
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@IsResetWatchStatusForUser", 1);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch(Exception ex)
            {
                Log.Information("Error in ResetWatchStatusForUser: " + ex.Message);
            }
        }


        /// <summary>
        /// Retrieves the identifiers of all queue entries with a status of 1.
        /// </summary>
        public static List<int> GetQueueEntriesByStatus()
        {
            List<int> Ids = new List<int>();
            
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();


                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@IsGetQueueEntriesByStatus", 1);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            

                            while (reader.Read())
                            {
                                Ids.Add(reader.GetInt32(0));
                            }
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetQueueEntriesByStatus: " + ex.Message);
            }

            return Ids;
        }

        /// <summary>
        /// Gets the number of directories with a pending status in the backup system.
        /// </summary>
        public static int GetPendingDirectoryCount()
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetPendingDirectoryCount", 1);
                        cmd.CommandTimeout = 60;
                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetPendingDirectoryCount: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Gets the number of items in the queue that are pending processing.
        /// </summary>
        public static int GetPendingQueueItemCount()
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@IsGetPendingQueueItemCount", 1);

                        object result = cmd.ExecuteScalar();

                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetPendingQueueItemCount: " + ex.Message);
                return 0;
            }
        }
    }
}