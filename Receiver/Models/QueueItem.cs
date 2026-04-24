using System;
using System.Collections.Generic;
using System.Text;

namespace Receiver.Models
{
    public class QueueItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int DirectoryId { get; set; }
        public long ObjectId { get; set; }
    }
}
