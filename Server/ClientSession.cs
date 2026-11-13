using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ClientSession
    {
        public string Token;
        public string Ip;
        public int Port;
        public DateTime ConnectTime;
        public DateTime TokenCreatedTime;
        public bool isKicked;
    }
}
