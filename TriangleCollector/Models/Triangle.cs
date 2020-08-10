using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public class Triangle
    {
        public string FirstSymbol { get; set; }

        public decimal FirstSymbolAsk { get; set; }

        public double FirstSymbolVolume { get; set; }

        public string SecondSymbol { get; set; }

        public decimal SecondSymbolBid { get; set; }

        public double SecondSymbolBidVolume { get; set; }

        public decimal SecondSymbolAsk { get; set; }

        public double SecondSymbolAskVolume { get; set; }

        public string ThirdSymbol { get; set; }

        public decimal ThirdSymbolBid { get; set; }

        public double ThirdSymbolVolume { get; set; }

        public decimal Profitability { get; set; }

        public bool AllPricesSet
        {
            get
            {
                return FirstSymbolAsk != 0 && SecondSymbolAsk != 0 && SecondSymbolBid != 0 && ThirdSymbolBid != 0;
            }
        }

        public Triangle(string FirstSymbol, string SecondSymbol, string ThirdSymbol)
        {
            this.FirstSymbol = FirstSymbol;
            this.SecondSymbol = SecondSymbol;
            this.ThirdSymbol = ThirdSymbol;
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var pair in new List<string> { FirstSymbol, SecondSymbol, ThirdSymbol })
            {
                yield return pair;
            }
        }

        public override string ToString()
        {
            return $"{FirstSymbol}-{SecondSymbol}-{ThirdSymbol}";
        }

        public string ToReversedString()
        {
            return $"{ThirdSymbol}-{SecondSymbol}-{FirstSymbol}";
        }

        public decimal GetProfitability()
        {
            try
            {
                var firstTrade = 1 / FirstSymbolAsk;
                var secondTrade = firstTrade * SecondSymbolBid;
                var thirdTrade = secondTrade * ThirdSymbolBid;
                this.Profitability = thirdTrade;
                return thirdTrade;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        public decimal GetReversedProfitability()
        {
            return 0;
        }
    }
}
