using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public class Ask
    {
        public string price { get; set; }
        public string size { get; set; }

    }

    public class Bid
    {
        public string price { get; set; }
        public string size { get; set; }

    }

    public class Params
    {
        public List<Ask> ask { get; set; }
        public List<Bid> bid { get; set; }
        public string symbol { get; set; }
        public int sequence { get; set; }
        public DateTime timestamp { get; set; }

    }

    public class Orderbook
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public Params @params { get; set; }
    }
}



