using System;
using System.Collections.Generic;
using System.Text;

namespace HubSpotBackupSystem.Models
{
    public class HubspotTokenResponse
    {
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
        public int expireAt { get; set; }
    }
}
