using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public class Triangle
    {
        public string Base { get; set; }

        public decimal BaseAsk { get; set; }

        public double BaseVolume { get; set; }

        public string Middle { get; set; }

        public decimal MiddleBid { get; set; }

        public double MiddleVolume { get; set; }

        public string Final { get; set; }

        public decimal FinalBid { get; set; }

        public double FinalVolume { get; set; }
    }
}
