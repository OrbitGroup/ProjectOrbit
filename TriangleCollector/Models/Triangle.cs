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

        KeyValuePair<int, decimal> MaxVolume = new KeyValuePair<int, decimal>();

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
            return $"{Direction[0]} {FirstSymbol} bid price {FirstSymbolOrderbook.SortedBids.First().Key} bid depth {FirstSymbolOrderbook.SortedBids.First().Value} ask price {FirstSymbolOrderbook.SortedAsks.First().Key} ask depth {FirstSymbolOrderbook.SortedAsks.First().Value} \n" +
                $"--{Direction[1]} {SecondSymbol} bid price {SecondSymbolOrderbook.SortedBids.First().Key} bid depth {SecondSymbolOrderbook.SortedBids.First().Value} ask price {SecondSymbolOrderbook.SortedAsks.First().Key} ask depth {SecondSymbolOrderbook.SortedAsks.First().Value} \n" +
                $"--{Direction[2]} {ThirdSymbol} bid price {ThirdSymbolOrderbook.SortedBids.First().Key} bid depth {ThirdSymbolOrderbook.SortedBids.First().Value} ask price {ThirdSymbolOrderbook.SortedAsks.First().Key} ask depth {ThirdSymbolOrderbook.SortedAsks.First().Value} \n" +
                $"maximum volume is {MaxVolume.Value} BTC, bottleneck is trade #{MaxVolume.Key}";
        }

        private decimal GetProfitPercent(Orderbook first, Orderbook second, Orderbook third, List<String> Direction)
        {
            try //use the direction list to understand what trades to make at each step
            {
                if (Direction[0] == "Buy") //two directions start with buying: "Buy Sell Sell" and "Buy Buy Sell"
                {
                    var firstTrade = 1 / first.SortedAsks.First().Key;
                    if (Direction[1] == "Buy") //must be "Buy Buy Sell"
                    {
                        var secondTrade = firstTrade / second.SortedAsks.First().Key; //buy
                        var thirdTrade = secondTrade * third.SortedBids.First().Key; //sell
                        return thirdTrade;
                    }
                    else //must be "Buy Sell Sell"
                    {
                        var secondTrade = firstTrade * second.SortedBids.First().Key; //sell
                        var thirdTrade = secondTrade * third.SortedBids.First().Key; //sell
                        return thirdTrade;
                    }
                }
                else // only one direction starts with selling: "Sell Buy Sell"
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
            MaxVolume = GetMaxVolume(FirstSymbolOrderbook, SecondSymbolOrderbook, ThirdSymbolOrderbook, Direction);

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

        private KeyValuePair<int, decimal> GetMaxVolume(Orderbook FirstSymbolOrderbook, Orderbook SecondSymbolOrderbook, Orderbook ThirdSymbolOrderbook, List<String> Direction)
        {
            if (Direction[0] == "Buy")
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedAsks.First().Key * FirstSymbolOrderbook.SortedAsks.First().Value;
                if (Direction[1] == "Buy")
                {
                    // the second trade is in the other base's terms, so you must convert the base-terms volume into BTC using the first trade price (which is base-BTC) 
                    // Other than that, the logic is the same as the first trade since we are buying something again.
                    decimal secondBtcVolume = SecondSymbolOrderbook.SortedAsks.First().Key * SecondSymbolOrderbook.SortedAsks.First().Value * FirstSymbolOrderbook.SortedBids.First().Key;
                    // the third direction must be Sell at this point (there is no other potential combination)
                    decimal thirdBtcVolume = ThirdSymbolOrderbook.SortedBids.First().Key * ThirdSymbolOrderbook.SortedBids.First().Value;
                    //calculate and identify the bottleneck
                    if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                    {
                        return new KeyValuePair<int, decimal>(1, firstBtcVolume);
                    }
                    else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                    {
                        return new KeyValuePair<int, decimal>(2, secondBtcVolume);
                    }
                    else
                        return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
                else
                {
                    // second trade is a sell order, so the direction must be Buy Sell Sell
                    // the depth is expressed in altcoin terms which must be converted to BTC. Price is expressed in basecoin terms.
                    // the first order book contains the ALT-BTC price, which is therefore used to convert the volume to BTC terms
                    decimal secondBtcVolume = SecondSymbolOrderbook.SortedBids.First().Key * SecondSymbolOrderbook.SortedBids.First().Value * ThirdSymbolOrderbook.SortedBids.First().Key;
                    // third trade is always in BTC price terms
                    decimal thirdBtcVolume = ThirdSymbolOrderbook.SortedBids.First().Key * ThirdSymbolOrderbook.SortedBids.First().Value;
                    //calculate and identify the bottleneck
                    if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                    {
                        return new KeyValuePair<int, decimal>(1, firstBtcVolume);
                    }
                    else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                    {
                        return new KeyValuePair<int, decimal>(2, secondBtcVolume);
                    }
                    else
                        return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }
            else
            {
                //first trade is a sell order. only one direction starts with a sell order: Sell Buy Sell
                //the only scenario when the first trade is a sell order is USDT/TUSD based trades, in which depth is already expressed in BTC (price is expressed in USD)
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedBids.First().Value;
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = SecondSymbolOrderbook.SortedAsks.First().Value * ThirdSymbolOrderbook.SortedBids.First().Key;
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.SortedBids.First().Key * ThirdSymbolOrderbook.SortedBids.First().Value;
                //calculate and identify the bottleneck
                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<int, decimal>(1, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<int, decimal>(2, secondBtcVolume);
                }
                else
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
            }
        }
    }
}
            
            
            
            
            
            

