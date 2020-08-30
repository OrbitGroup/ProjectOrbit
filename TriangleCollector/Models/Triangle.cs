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

        public Directions Direction;

        public enum Directions
        {
            BuyBuySell,
            SellBuySell,
            BuySellSell
        }

        private readonly object nextLayerLock = new object();

        private ILogger<Triangle> _logger;

        public bool AllOrderbooksSet
        {
            get
            {
                return FirstSymbolOrderbook != null && SecondSymbolOrderbook != null && ThirdSymbolOrderbook != null;
            }
        }

        public Triangle(string FirstSymbol, string SecondSymbol, string ThirdSymbol, Directions Direction, ILogger<Triangle> logger)
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
            return $"{FirstSymbol}-{SecondSymbol}-{ThirdSymbol}";
        }

        public void SetMaxVolumeAndProfitability()
        {
            lock (nextLayerLock)
            {
                if (FirstSymbolOrderbook.SortedAsks == null || FirstSymbolOrderbook.SortedBids == null || SecondSymbolOrderbook.SortedBids == null || SecondSymbolOrderbook.SortedAsks == null || ThirdSymbolOrderbook.SortedBids == null)
                {
                    return;
                }
                FirstSymbolAsks.Clear();
                FirstSymbolBids.Clear();

                SecondSymbolAsks.Clear();
                SecondSymbolBids.Clear();

                ThirdSymbolBids.Clear();

                if (Direction == Directions.BuyBuySell)
                {
                    FirstSymbolAsks.Add(FirstSymbolOrderbook.SortedAsks.First());
                    SecondSymbolAsks.Add(SecondSymbolOrderbook.SortedAsks.First());
                    ThirdSymbolBids.Add(ThirdSymbolOrderbook.SortedBids.First());
                }
                else if (Direction == Directions.BuySellSell)
                {
                    FirstSymbolAsks.Add(FirstSymbolOrderbook.SortedAsks.First());
                    SecondSymbolBids.Add(SecondSymbolOrderbook.SortedBids.First());
                    ThirdSymbolBids.Add(ThirdSymbolOrderbook.SortedBids.First());
                }
                else
                {
                    FirstSymbolBids.Add(FirstSymbolOrderbook.SortedBids.First());
                    SecondSymbolAsks.Add(SecondSymbolOrderbook.SortedAsks.First());
                    ThirdSymbolBids.Add(ThirdSymbolOrderbook.SortedBids.First());
                }

                ProfitPercent = GetProfitPercent();

                var maxVol = GetMaxVolume();
                decimal volumeTraded = maxVol.Value;

                if (ProfitPercent < 1)
                {
                    //_logger.LogDebug($"first: {FirstSymbolOrderbook.symbol} Price: {FirstSymbolAsks.Last().Key} depth: {FirstSymbolAsks.Last().Value} second: {SecondSymbolOrderbook.symbol} Price: {SecondSymbolBids.Last().Key} depth: {SecondSymbolBids.Last().Value} third: {ThirdSymbolOrderbook.symbol} Price: {ThirdSymbolBids.Last().Key} depth: {ThirdSymbolBids.Last().Value}");
                    //_logger.LogDebug($"{minVol.Value} maximum BTC of volume calculated");
                    this.MaxVolume = volumeTraded;
                    this.Profit = 0;
                    return;
                }

                decimal profitReturned = 0;
                while (true)
                {

                    KeyValuePair<decimal, decimal> nextLayer;
                    decimal newProfitPercent;
                    if (maxVol.Key == 1)
                    {

                        if (Direction == Directions.BuyBuySell)
                        {

                            nextLayer = FirstSymbolOrderbook.SortedAsks.ElementAt(FirstSymbolAsks.Count);
                            newProfitPercent = GetProfitPercent(nextLayer.Key, SecondSymbolAsks.Last().Key, ThirdSymbolBids.Last().Key);

                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                FirstSymbolAsks.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                        else if (Direction == Directions.BuySellSell)
                        {
                            nextLayer = FirstSymbolOrderbook.SortedAsks.ElementAt(FirstSymbolAsks.Count);
                            newProfitPercent = GetProfitPercent(nextLayer.Key, SecondSymbolBids.Last().Key, ThirdSymbolBids.Last().Key);

                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                FirstSymbolAsks.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                        else //sell buy sell
                        {

                            nextLayer = FirstSymbolOrderbook.SortedBids.ElementAt(FirstSymbolBids.Count);
                            newProfitPercent = GetProfitPercent(nextLayer.Key, SecondSymbolAsks.Last().Key, ThirdSymbolBids.Last().Key);

                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                FirstSymbolBids.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                    }
                    else if (maxVol.Key == 2)
                    {
                        if (Direction == Directions.BuyBuySell)
                        {
                            nextLayer = SecondSymbolOrderbook.SortedAsks.ElementAt(SecondSymbolAsks.Count);
                            newProfitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, nextLayer.Key, ThirdSymbolBids.Last().Key);
                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                SecondSymbolAsks.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                        else if (Direction == Directions.BuySellSell)
                        {
                            nextLayer = SecondSymbolOrderbook.SortedBids.ElementAt(SecondSymbolBids.Count);
                            newProfitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, nextLayer.Key, ThirdSymbolBids.Last().Key);
                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                SecondSymbolBids.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                        else //sell buy sell
                        {
                            nextLayer = SecondSymbolOrderbook.SortedAsks.ElementAt(SecondSymbolAsks.Count);
                            newProfitPercent = GetProfitPercent(FirstSymbolBids.Last().Key, nextLayer.Key, ThirdSymbolBids.Last().Key);
                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                SecondSymbolAsks.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                    }
                    else //maxVol.Key == 3
                    {
                        if (Direction == Directions.BuyBuySell)
                        {
                            if (ThirdSymbolBids.Count >= ThirdSymbolOrderbook.SortedBids.Count())
                            {
                                return;
                            }
                            nextLayer = ThirdSymbolOrderbook.SortedBids.ElementAt(ThirdSymbolBids.Count);
                            newProfitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, SecondSymbolAsks.Last().Key, nextLayer.Key);
                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                ThirdSymbolBids.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                        else if (Direction == Directions.BuySellSell)
                        {
                            if (ThirdSymbolBids.Count >= ThirdSymbolOrderbook.SortedBids.Count())
                            {
                                return;
                            }
                            nextLayer = ThirdSymbolOrderbook.SortedBids.ElementAt(ThirdSymbolBids.Count);
                            newProfitPercent = GetProfitPercent(FirstSymbolAsks.Last().Key, SecondSymbolBids.Last().Key, nextLayer.Key);
                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                ThirdSymbolBids.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                        else //sell buy sell
                        {
                            nextLayer = ThirdSymbolOrderbook.SortedBids.ElementAt(ThirdSymbolBids.Count);
                            newProfitPercent = GetProfitPercent(FirstSymbolBids.Last().Key, SecondSymbolAsks.Last().Key, nextLayer.Key);
                            if (newProfitPercent < 1)
                            {
                                break;
                            }
                            else
                            {
                                ThirdSymbolBids.Add(nextLayer);
                                ProfitPercent = newProfitPercent;
                                volumeTraded += maxVol.Value;
                                profitReturned += maxVol.Value * ProfitPercent;
                            }
                        }
                    }
                }
                if (volumeTraded != 0)
                {
                    ProfitPercent = profitReturned / volumeTraded;
                    MaxVolume = volumeTraded;
                    this.Profit = profitReturned;
                }
            }
        }

        private decimal GetProfitPercent()
        {
            try //use the direction list to understand what trades to make at each step
            {
                if (Direction == Directions.BuySellSell)
                {
                    var firstTrade = 1 / FirstSymbolOrderbook.SortedAsks.First().Key;
                    var secondTrade = firstTrade * SecondSymbolOrderbook.SortedBids.First().Key; //sell
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key; //sell
                    return thirdTrade;
                }
                else if (Direction == Directions.BuyBuySell)
                {
                    var firstTrade = 1 / FirstSymbolOrderbook.SortedAsks.First().Key;
                    var secondTrade = firstTrade / SecondSymbolOrderbook.SortedAsks.First().Key; //buy
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key; //sell
                    return thirdTrade;
                }
                else //Sell Buy Sell
                {
                    var firstTrade = 1 * FirstSymbolOrderbook.SortedBids.First().Key;
                    var secondTrade = firstTrade / SecondSymbolOrderbook.SortedAsks.First().Key;
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key;
                    return thirdTrade;
                }
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        private decimal GetProfitPercent(decimal firstSymbolPrice, decimal secondSymbolPrice, decimal thirdSymbolPrice)
        {
            try //use the direction list to understand what trades to make at each step
            {
                if (Direction == Directions.BuySellSell)
                {
                    var firstTrade = 1 / firstSymbolPrice;
                    var secondTrade = firstTrade * secondSymbolPrice; //sell
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key; //sell
                    return thirdTrade;
                }
                else if (Direction == Directions.BuyBuySell)
                {
                    var firstTrade = 1 / firstSymbolPrice;
                    var secondTrade = firstTrade / secondSymbolPrice; //buy
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key; //sell
                    return thirdTrade;
                }
                else //Sell Buy Sell
                {
                    var firstTrade = 1 * firstSymbolPrice;
                    var secondTrade = firstTrade / secondSymbolPrice;
                    var thirdTrade = secondTrade * thirdSymbolPrice;
                    return thirdTrade;
                }

            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        private KeyValuePair<int, decimal> GetMaxVolume()
        {
            if (Direction == Directions.BuyBuySell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // the second trade is in the other base's terms, so you must convert the base-terms volume into BTC using the first trade price (which is base-BTC) 
                // Other than that, the logic is the same as the first trade since we are buying something again.
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedAsks.First().Key * FirstSymbolOrderbook.SortedAsks.First().Value;
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
                {
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // second trade is a sell order, so the direction must be Buy Sell Sell
                // the depth is expressed in altcoin terms which must be converted to BTC. Price is expressed in basecoin terms.
                // the first order book contains the ALT-BTC price, which is therefore used to convert the volume to BTC terms
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedAsks.First().Key * FirstSymbolOrderbook.SortedAsks.First().Value;
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
                {
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }
            else //Sell Buy Sell
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
                {
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }
        }

        private KeyValuePair<int, decimal> GetMaxVolume(KeyValuePair<decimal, decimal> firstSymbolLayer, KeyValuePair<decimal, decimal> secondSymbolLayer, KeyValuePair<decimal, decimal> thirdSymbolLayer)
        {
            if (Direction == Directions.BuyBuySell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // the second trade is in the other base's terms, so you must convert the base-terms volume into BTC using the first trade price (which is base-BTC) 
                // Other than that, the logic is the same as the first trade since we are buying something again.
                decimal firstBtcVolume = firstSymbolLayer.Key * firstSymbolLayer.Value;
                decimal secondBtcVolume = secondSymbolLayer.Key * secondSymbolLayer.Value * FirstSymbolOrderbook.SortedBids.First().Key;
                // the third direction must be Sell at this point (there is no other potential combination)
                decimal thirdBtcVolume = thirdSymbolLayer.Key * thirdSymbolLayer.Value;
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
                {
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // second trade is a sell order, so the direction must be Buy Sell Sell
                // the depth is expressed in altcoin terms which must be converted to BTC. Price is expressed in basecoin terms.
                // the first order book contains the ALT-BTC price, which is therefore used to convert the volume to BTC terms
                decimal firstBtcVolume = firstSymbolLayer.Key * firstSymbolLayer.Value;
                decimal secondBtcVolume = secondSymbolLayer.Key * secondSymbolLayer.Value * ThirdSymbolOrderbook.SortedBids.First().Key;
                // third trade is always in BTC price terms
                decimal thirdBtcVolume = thirdSymbolLayer.Key * thirdSymbolLayer.Value;
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
                {
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }
            else //Sell Buy Sell
            {
                //first trade is a sell order. only one direction starts with a sell order: Sell Buy Sell
                //the only scenario when the first trade is a sell order is USDT/TUSD based trades, in which depth is already expressed in BTC (price is expressed in USD)
                decimal firstBtcVolume = firstSymbolLayer.Value;
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = secondSymbolLayer.Value * ThirdSymbolOrderbook.SortedBids.First().Key;
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = thirdSymbolLayer.Key * thirdSymbolLayer.Value;
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
                {
                    return new KeyValuePair<int, decimal>(3, thirdBtcVolume);
                }
            }   
        }
    }
}
            
            
            
            
            
            

