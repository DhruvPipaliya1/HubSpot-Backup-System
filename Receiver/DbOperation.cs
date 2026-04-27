using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using Receiver.Models;
using Serilog;
using System.Data;

namespace Receiver
{
    public class DbOperation
    {
        /// <summary>
        /// Inserts a new entry into the FileBackup table and returns the unique identifier of the inserted record.
        /// </summary>
        public static int InsertHubspotEntryAndGetId(
        int userId,
        int directoryId,
        long objectId,
        string originalLocation,
        string hubSpotUrl,
        DateTime created,
        DateTime modified)
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
                        cmd.Parameters.AddWithValue("@IsInsertHubspotEntryAndGetId", 1);
                        cmd.Parameters.AddWithValue("@FileUserId", userId);
                        cmd.Parameters.AddWithValue("@FolderFId", directoryId);
                        cmd.Parameters.AddWithValue("@FileObjectId", objectId);
                        cmd.Parameters.AddWithValue("@OriginalLocation", originalLocation ?? "");
                        cmd.Parameters.AddWithValue("@hubSpotUrl", hubSpotUrl);
                        cmd.Parameters.AddWithValue("@CopyStatus", 1);
                        cmd.Parameters.AddWithValue("@Created", created);
                        cmd.Parameters.AddWithValue("@Modified", modified);
                        cmd.Parameters.AddWithValue("@FileDataSet", json);

                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex) { 
                Log.Information("Error in InsertHubspotEntryAndGetId" + ex.Message);
                return -1;
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
        /// Updates the native location value for a file backup record with the specified identifier.
        /// </summary>
        public static void UpdateNativeLocation(int id, string nativeLocation)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsUpdateNativeLocation", 1);
                        cmd.Parameters.AddWithValue("@NativeLocation", nativeLocation);
                        cmd.Parameters.AddWithValue("@FileId", id);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in UpdateNativeLocation: ", ex.Message);
            }
        }


        /// <summary>
        /// Retrieves a list of queue entries that match the specified queue item IDs.
        /// </summary>
        public static List<QueueItem> GetQueueEntriesByIds(List<int> ids)
        {
            var list = new List<QueueItem>();

            if (ids == null || ids.Count == 0)
                return list;

            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    string idParams = string.Join(",", ids);

                    string query = $"SELECT id, userId, directoryId, objectId FROM QueueTable WHERE id IN ({idParams})";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new QueueItem
                            {
                                Id = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                DirectoryId = reader.GetInt32(2),
                                ObjectId = long.Parse(reader.GetString(3))
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetQueueEntriesByIds: " + ex.Message);
            }

            return list;
        }


        /// <summary>
        /// Retrieves the most recent modification date for a file backup associated with the specified object
        /// identifier.
        /// </summary>
        public static DateTime? GetLastModifiedByObjectId(long objectId)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetLastModifiedByObjectId", 1);
                        cmd.Parameters.AddWithValue("@FileObjectId", objectId);

                        var result = cmd.ExecuteScalar();

                        if (result == null || result == DBNull.Value)
                            return null;  

                        return Convert.ToDateTime(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetLastModifiedByObjectId: " + ex.Message);
                return null;
            }
        }


        /// <summary>
        /// Updates the status of a queue entry identified by its unique identifier.
        /// </summary>
        public static void UpdateQueueEntryStatus(int id, int status)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();


                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsUpdateQueueEntryStatus", 1);
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@QueueId", id);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in UpdateQueueEntryStatus: ", ex.Message);
            }
        }


        /// <summary>
        /// Retrieves the object type associated with the specified directory identifier from the FolderBackup table.
        /// </summary>
        public static string GetObjectTypeByDirectoryId(int directoryId)
        {
            object result = null;
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();


                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetObjectTypeByDirectoryId", 1);
                        cmd.Parameters.AddWithValue("@FolderId", directoryId);

                        result = cmd.ExecuteScalar();

                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetObjectTypeByDirectoryId: ", ex.Message);
            }
            return result != null ? result.ToString() : null;
        }



        /// <summary>
        /// Retrieves the access token associated with the specified user identifier.
        /// </summary>
        public static string GetAccessToken(int userId)
        {
            object result = null;
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();


                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsGetAccessToken", 1);
                        cmd.Parameters.AddWithValue("@UuserId", userId);

                        result = cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in GetAccessToken: ", ex.Message);
            }

            return result != null ? result.ToString() : null;
        }



        /// <summary>
        /// Retrieves the search filter criteria for the specified user and object type.
        /// </summary>
        public static (string nameKeyword, DateTime? dateFrom, DateTime? dateTo) GetSearchFilter(int userId, string objectType)
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


        /// <summary>
        /// Updates the copy status of a file in the database using the specified file identifier and status value.
        /// </summary>
        public static void UpdateCopyStatus(int FileId, int Status)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand("HubspotBackupProcedure", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IsUpdateCopyStatus", 1);
                        cmd.Parameters.AddWithValue("@FileId", FileId);
                        cmd.Parameters.AddWithValue("@CopyStatus", Status);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in UpdateCopyStatus: " + ex.Message);
            }
        }


        /// <summary>
        /// Updates the file name and file size for the specified file backup record in the database.
        /// </summary>
        public static void UpdateFileNameAndSize(int id, string fileName, long fileSize)
        {
            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand(@"
                UPDATE FileBackup 
                SET fileName = @fileName, fileSize = @fileSize 
                WHERE id = @id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@fileName", fileName);
                        cmd.Parameters.AddWithValue("@fileSize", fileSize);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error in UpdateFileNameAndSize: " + ex.Message);
            }
        }
    }
}