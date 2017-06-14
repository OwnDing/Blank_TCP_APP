using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blank_TCP_Server.Methods.AsyncAwaitServer
{
    public class Message
    {
        public string data{get;set;}
        public string ip { get; set; }

        public override string ToString()
        {
            return "Data:"+data+"  From:"+ip;
        }
    }
}
