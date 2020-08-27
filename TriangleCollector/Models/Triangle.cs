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

        public List<KeyValuePair<decimal, decimal>> FirstSymbolBids = new List<KeyValuePair<decimal, decimal>>();

        public string SecondSymbol { get; set; }

        public Orderbook SecondSymbolOrderbook { get; set; }

        public List<KeyValuePair<decimal, decimal>> SecondSymbolBids = new List<KeyValuePair<decimal, decimal>>();

        public List<KeyValuePair<decimal, decimal>> SecondSymbolAsks = new List<KeyValuePair<decimal, decimal>>();

        public string ThirdSymbol { get; set; }

        public Orderbook ThirdSymbolOrderbook { get; set; }

        public List<KeyValuePair<decimal, decimal>> ThirdSymbolBids = new List<KeyValuePair<decimal, decimal>>();

        public decimal ProfitPercent { get; set; }

        public decimal Profit { get; set; }

        public decimal MaxVolume { get; set; }

        public List<string> Direction = new List<string>();

        private ILogger<Triangle> _logger;

        public bool AllOrderbooksSet
        {
            get
            {
                return FirstSymbolOrderbook != null && SecondSymbolOrderbook != null && ThirdSymbolOrderbook != null;
            }
        }

        public Triangle(string FirstSymbol, string SecondSymbol, string ThirdSymbol, List<String> Direction, ILogger<Triangle> logger)
        {
            this.FirstSymbol = FirstSymbol;
            this.SecondSymbol = SecondSymbol;
            this.ThirdSymbol = ThirdSymbol;
            this.Direction = Direction;
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
            return $"{Direction[0]} {FirstSymbol}--{Direction[1]} {SecondSymbol}--{Direction[2]} {ThirdSymbol}";
        }

        public string ToReversedString()
        {
            return $"{ThirdSymbol}-{SecondSymbol}-{FirstSymbol}";
        }

        private decimal GetProfitPercent(Orderbook first, Orderbook second, Orderbook third, List<String> Direction)
        {
            try //use the direction list to understand what trades to make at each step
            {
                if (Direction[0] == "Buy") //two directions start with buying: "Buy Sell Sell" and "Buy Buy Sell"
                {
                    var firstTrade = 1 / first.SortedAsks.First().Key;
                    if (Direction [1] == "Buy") //must be "Buy Buy Sell"
                    {
                        var secondTrade = firstTrade / second.SortedAsks.First().Key; //buy
                        var thirdTrade = secondTrade * third.SortedBids.First().Key; //sell
                        return thirdTrade;
                    } else //must be "Buy Sell Sell"
                    {
                        var secondTrade = firstTrade * second.SortedBids.First().Key; //sell
                        var thirdTrade = secondTrade * third.SortedBids.First().Key; //sell
                        return thirdTrade;
                    }
                } else // only one direction starts with selling: "Sell Buy Sell"
                {
                    var firstTrade = 1 * first.SortedBids.First().Key;
                    var secondTrade = firstTrade / second.SortedAsks.First().Key;
                    var thirdTrade = secondTrade * third.SortedBids.First().Key;
                    return thirdTrade;
                }

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

            ProfitPercent = GetProfitPercent(FirstSymbolOrderbook, SecondSymbolOrderbook, ThirdSymbolOrderbook, Direction);
            return;
            /*if (profitPercent < 1)
            {
                var minVol = GetMinVolume(FirstSymbolAsks.Last(), SecondSymbolBids.Last(), ThirdSymbolBids.Last());
                //_logger.LogDebug($"first: {FirstSymbolOrderbook.symbol} Price: {FirstSymbolAsks.Last().Key} depth: {FirstSymbolAsks.Last().Value} second: {SecondSymbolOrderbook.symbol} Price: {SecondSymbolBids.Last().Key} depth: {SecondSymbolBids.Last().Value} third: {ThirdSymbolOrderbook.symbol} Price: {ThirdSymbolBids.Last().Key} depth: {ThirdSymbolBids.Last().Value}");
                //_logger.LogDebug($"{minVol.Value} maximum BTC of volume calculated");
                
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

            ProfitPercent = profitReturned / volumeTraded; //This seems to be broken...
            this.Profit = profitReturned;
*/
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
            else
            return new KeyValuePair<int, decimal>(3, thirdBtcVol);
        }

        public decimal GetReversedProfitability()
        {
            return 0;
        }
    }
}
