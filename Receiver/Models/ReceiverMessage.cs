using System;
using System.Collections.Generic;
using System.Text;

namespace Receiver.Models
{
    public class ReceiverMessage
    {
        public int userId {  get; set; }

        public List<int> Ids { get; set; }
    }
}
