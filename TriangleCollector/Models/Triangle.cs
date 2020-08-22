using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

        public Orderbook FirstSymbolOrderbook { get; set; }

        public List<KeyValuePair<decimal, decimal>> FirstSymbolAsks = new List<KeyValuePair<decimal, decimal>>();

        public string SecondSymbol { get; set; }

        public Orderbook SecondSymbolOrderbook { get; set; }

        public List<KeyValuePair<decimal, decimal>> SecondSymbolBids = new List<KeyValuePair<decimal, decimal>>();

        public string ThirdSymbol { get; set; }

        public Orderbook ThirdSymbolOrderbook { get; set; }

        public List<KeyValuePair<decimal, decimal>> ThirdSymbolBids = new List<KeyValuePair<decimal, decimal>>();

        public decimal ProfitPercent { get; set; }

        public decimal Profit { get; set; }

        public decimal MaxVolume { get; set; }

        private ILogger<Triangle> _logger;

        public bool AllOrderbooksSet
        {
            get
            {
                return FirstSymbolOrderbook != null && SecondSymbolOrderbook != null && ThirdSymbolOrderbook != null;
            }
        }

        public Triangle(string FirstSymbol, string SecondSymbol, string ThirdSymbol, ILogger<Triangle> logger)
        {
            this.FirstSymbol = FirstSymbol;
            this.SecondSymbol = SecondSymbol;
            this.ThirdSymbol = ThirdSymbol;
            _logger = logger;
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

        private decimal GetProfitPercent(decimal firstAsk, decimal secondBid, decimal thirdBid)
        {
            try
            {

                var firstTrade = 1 / firstAsk;
                var secondTrade = firstTrade * secondBid;
                var thirdTrade = secondTrade * thirdBid;

                //get first layer profit + volume
                //get symbol with lowest volume
                //determine if second layer of lowVol symbol is profitable
                //repeat

                return thirdTrade;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        public void SetMaxVolumeAndProfitability()
        {
            if (FirstSymbolOrderbook.SortedAsks == null || SecondSymbolOrderbook.SortedBids == null || ThirdSymbolOrderbook.SortedBids == null)
            {
                return;
            }
            FirstSymbolAsks.Clear();
            SecondSymbolBids.Clear();
            ThirdSymbolBids.Clear();

            FirstSymbolAsks.Add(FirstSymbolOrderbook.SortedAsks.First());
            SecondSymbolBids.Add(SecondSymbolOrderbook.SortedBids.First());
            ThirdSymbolBids.Add(ThirdSymbolOrderbook.SortedBids.First());

            var profitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, SecondSymbolBids.Last().Key, ThirdSymbolBids.Last().Key);
            if (profitPercent < 1)
            {
                this.ProfitPercent = profitPercent;
                this.Profit = 0;
                return;
            }

            decimal volumeTraded = 0;
            decimal profitReturned = 0;
            while (true)
            {
                var minVol = GetMinVolume(FirstSymbolAsks.Last(), SecondSymbolBids.Last(), ThirdSymbolBids.Last());

                if (minVol.Key == 1)
                {
                    var nextLayer = FirstSymbolOrderbook.SortedAsks.ElementAt(FirstSymbolAsks.Count);
                    var newProfitPercent = GetProfitPercent(nextLayer.Key, SecondSymbolBids.Last().Key, ThirdSymbolBids.Last().Key);
                    if (newProfitPercent < 1)
                    {
                        break;
                    }
                    else
                    {
                        FirstSymbolAsks.Add(nextLayer);
                        profitPercent = newProfitPercent;
                        volumeTraded += minVol.Value;
                        profitReturned += volumeTraded * profitPercent;
                    }
                }
                else if (minVol.Key == 2)
                {
                    var nextLayer = SecondSymbolOrderbook.SortedBids.ElementAt(SecondSymbolBids.Count);
                    var newProfitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, nextLayer.Key, ThirdSymbolBids.Last().Key);
                    if (newProfitPercent < 1)
                    {
                        break;
                    }
                    else
                    {
                        SecondSymbolBids.Add(nextLayer);
                        profitPercent = newProfitPercent;
                        volumeTraded += minVol.Value;
                        profitReturned += volumeTraded * profitPercent;
                    }
                }
                else
                {
                    var nextLayer = ThirdSymbolOrderbook.SortedBids.ElementAt(ThirdSymbolBids.Count);
                    var newProfitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, SecondSymbolBids.Last().Key, nextLayer.Key);
                    if (newProfitPercent < 1)
                    {
                        break;
                    }
                    else
                    {
                        ThirdSymbolBids.Add(nextLayer);
                        profitPercent = newProfitPercent;
                        volumeTraded += minVol.Value;
                        profitReturned += volumeTraded * profitPercent;
                    }
                }
            }

            this.ProfitPercent = profitReturned / volumeTraded; //This seems to be broken...
            this.Profit = profitReturned;

            //determine highest profitability but maximizing volume to an extent
        }

        // the first market's volume is quoted in altcoin terms. The 'volume' expressed for buying ICXBTC will be the number of ICX available for purchase
        // the second market's volume is quoted in altcoin terms. The 'volume' expressed for selling ICXETH will be the number of ICX available to be sold for ETH
        // the third market's volume is quoted in altcoin terms. The 'volume' expressed for selling ETHBTC will be the number of ETH available to be sold for BTC

        private KeyValuePair<int, decimal> GetMinVolume(KeyValuePair<decimal, decimal> firstAsk, KeyValuePair<decimal, decimal> secondBid, KeyValuePair<decimal, decimal> thirdBid)
        {
            
            decimal firstBtcVol = decimal.MaxValue;
            decimal secondBtcVol = decimal.MaxValue;

            if (firstAsk.Value <= secondBid.Value)
            {
                firstBtcVol = firstAsk.Key * firstAsk.Value;
            }
            else
            {
                secondBtcVol = secondBid.Key * secondBid.Value * thirdBid.Key;
            }

            var thirdBtcVol = thirdBid.Key * thirdBid.Value;

            if (firstBtcVol <= secondBtcVol && firstBtcVol <= thirdBtcVol)
            {
                return new KeyValuePair<int, decimal>(1, firstBtcVol);
            }
            else if (secondBtcVol <= firstBtcVol && secondBtcVol <= thirdBtcVol)
            {
                return new KeyValuePair<int, decimal>(2, secondBtcVol);
            }

            return new KeyValuePair<int, decimal>(3, thirdBtcVol);
        }

        public decimal GetReversedProfitability()
        {
            return 0;
        }
    }
}
