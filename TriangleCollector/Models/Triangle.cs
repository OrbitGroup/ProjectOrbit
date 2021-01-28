using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Models
{
    public class Triangle
    {
        public IExchange Exchange { get; set; }

        public string FirstSymbol { get; set; }
        public int FirstSymbolLayers { get; set; }

        public IOrderbook FirstSymbolOrderbook { get; set; }

        public Dictionary<decimal, decimal> FirstOrderBook { get; set; }
        public KeyValuePair<decimal, decimal> FirstOrderBookVolumeConverter { get; set; }
        public Dictionary<decimal, decimal> SecondOrderBook { get; set; }
        public Dictionary<decimal, decimal> ThirdOrderBook { get; set; }
        public KeyValuePair<decimal, decimal> ThirdOrderBookVolumeConverter { get; set; }

        public string SecondSymbol { get; set; }
        public int SecondSymbolLayers { get; set; }

        public IOrderbook SecondSymbolOrderbook { get; set; }

        public string ThirdSymbol { get; set; }
        public int ThirdSymbolLayers { get; set; }

        public IOrderbook ThirdSymbolOrderbook { get; set; }

        public decimal ProfitPercent { get; set; }

        public decimal Profit { get; set; }

        public decimal MaxVolume { get; set; }

        public long CreateSnapshotTime { get; set; }

        public Directions Direction;

        public enum Directions
        {
            BuyBuySell,
            SellBuySell,
            BuySellSell
        }

        public enum Bottlenecks
        {
            FirstTrade,
            SecondTrade,
            ThirdTrade
        }

        private ILogger<Triangle> _logger;

        public Triangle(string FirstSymbol, string SecondSymbol, string ThirdSymbol, Directions Direction, IExchange exch)
        {
            this.FirstSymbol = FirstSymbol;
            this.SecondSymbol = SecondSymbol;
            this.ThirdSymbol = ThirdSymbol;
            this.Direction = Direction;
            _logger = new LoggerFactory().CreateLogger<Triangle>();
            Exchange = exch;
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

        public bool NoEmptyOrderbooks 
        {
            get
            {
                if (FirstOrderBook.Count == 0 || SecondOrderBook.Count == 0 || ThirdOrderBook.Count == 0) //the third trade is always a bid
                {
                    return false;
                } else
                {
                    return true;
                }
                
            }
        }

        public bool SetMaxVolumeAndProfitability(IOrderbook firstSymbolOrderbook, IOrderbook secondSymbolOrderbook, IOrderbook thirdSymbolOrderbook)
        {
            try 
            {
                bool firstOrderbookEntered = Monitor.TryEnter(firstSymbolOrderbook.OrderbookLock, TimeSpan.FromMilliseconds(5));
                if (!firstOrderbookEntered)
                {
                    return false;
                }

                bool secondOrderbookEntered = Monitor.TryEnter(secondSymbolOrderbook.OrderbookLock, TimeSpan.FromMilliseconds(5));
                if (!secondOrderbookEntered)
                {
                    return false;
                }

                bool thirdOrderbookEntered = Monitor.TryEnter(thirdSymbolOrderbook.OrderbookLock, TimeSpan.FromMilliseconds(5));
                if (!thirdOrderbookEntered)
                {
                    return false;
                }
                
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                CreateOrderbookSnapshots(firstSymbolOrderbook, secondSymbolOrderbook, thirdSymbolOrderbook);
                stopwatch.Stop();
                CreateSnapshotTime = stopwatch.ElapsedMilliseconds;
                stopwatch.Reset();
            }
            finally
            {
                if (Monitor.IsEntered(firstSymbolOrderbook.OrderbookLock)) Monitor.Exit(firstSymbolOrderbook.OrderbookLock);
                if (Monitor.IsEntered(secondSymbolOrderbook.OrderbookLock)) Monitor.Exit(secondSymbolOrderbook.OrderbookLock);
                if (Monitor.IsEntered(thirdSymbolOrderbook.OrderbookLock)) Monitor.Exit(thirdSymbolOrderbook.OrderbookLock);
            }

            SetMaxVolumeAndProfitability();
            return true;
        }
        public void CreateOrderbookSnapshots(IOrderbook firstSymbolOrderbook, IOrderbook secondSymbolOrderbook, IOrderbook thirdSymbolOrderbook)
        {
            
            if (Direction == Directions.BuyBuySell)
            {
                FirstOrderBook = new Dictionary<decimal, decimal>(firstSymbolOrderbook.OfficialAsks);
                SecondOrderBook = new Dictionary<decimal, decimal>(secondSymbolOrderbook.OfficialAsks);
                FirstOrderBookVolumeConverter = new KeyValuePair<decimal, decimal>(firstSymbolOrderbook.OfficialBids.Keys.Max(),firstSymbolOrderbook.OfficialBids[firstSymbolOrderbook.OfficialBids.Keys.Max()]);
            }
            else if (Direction == Directions.BuySellSell)
            {
                FirstOrderBook = new Dictionary<decimal, decimal>(firstSymbolOrderbook.OfficialAsks);
                SecondOrderBook = new Dictionary<decimal, decimal>(secondSymbolOrderbook.OfficialBids);
                ThirdOrderBookVolumeConverter = new KeyValuePair<decimal, decimal>(thirdSymbolOrderbook.OfficialAsks.Keys.Min(), thirdSymbolOrderbook.OfficialAsks[thirdSymbolOrderbook.OfficialAsks.Keys.Min()]);
            }
            else //SellBuySell
            {
                FirstOrderBook = new Dictionary<decimal, decimal>(firstSymbolOrderbook.OfficialBids);
                SecondOrderBook = new Dictionary<decimal, decimal>(secondSymbolOrderbook.OfficialAsks);
         
            }
            ThirdOrderBook = new Dictionary<decimal, decimal>(thirdSymbolOrderbook.OfficialBids);
        }

        public void SetMaxVolumeAndProfitability()
        {
            MaxVolume = 0;
            Profit = 0;
            FirstSymbolLayers = 0;
            SecondSymbolLayers = 0;
            ThirdSymbolLayers = 0;

            while (NoEmptyOrderbooks)
            {
                var newProfitPercent = GetProfitPercent();

                var maxVol = GetMaxVolume();

                if (newProfitPercent > 0)
                {
                    if(NoEmptyOrderbooks)
                    {
                        RemoveLiquidity(maxVol);
                    }
                    MaxVolume += maxVol.Value;
                    Profit += maxVol.Value * newProfitPercent;
                    ProfitPercent = Profit / MaxVolume;
                }
                else
                {
                    MapResultstoSymbols();
                    if (ProfitPercent == 0)
                    {
                        ProfitPercent = newProfitPercent;
                        MaxVolume = maxVol.Value;
                    }
                    else if (MaxVolume == 0)
                    {
                        MaxVolume = maxVol.Value;
                    }
                    break;
                }
            }
            
        }

        public void MapResultstoSymbols()
        {
            //if the triangle is profitable, map each symbol to the ProfitableSymbolMapping dictionary along with the number of layers.
            //check to see if the number of layers for this opportunity is higher than the current maximum stored. If so, update that key value pair.
            if (Profit > 0)
            {
                Exchange.ProfitableSymbolMapping.AddOrUpdate(FirstSymbol, FirstSymbolLayers, (key, oldValue) => Math.Max(oldValue, FirstSymbolLayers));
                Exchange.ProfitableSymbolMapping.AddOrUpdate(SecondSymbol, SecondSymbolLayers, (key, oldValue) => Math.Max(oldValue, SecondSymbolLayers));
                Exchange.ProfitableSymbolMapping.AddOrUpdate(ThirdSymbol, ThirdSymbolLayers, (key, oldValue) => Math.Max(oldValue, ThirdSymbolLayers));
            } else
            {
                return;
            }
        }

        public void RemoveLiquidity(KeyValuePair<Bottlenecks, decimal> bottleneck)
        {
            //use min/max functions once to define the lowest layers up front as opposed to calling the min/max methods repeatedly (often 7/8 times per layer) throughout the process.
            var thirdSymbolHighestBidKey = ThirdOrderBook.Keys.Max();
            var firstSymbolLowestAskKey = FirstOrderBook.Keys.Min();
            var secondSymbolLowestAskKey = SecondOrderBook.Keys.Min();
            var firstSymbolHighestBidKey = FirstOrderBook.Keys.Max();

            if (Direction == Directions.BuyBuySell)
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstOrderBook.Remove(firstSymbolLowestAskKey, out var _);

                    SecondOrderBook[secondSymbolLowestAskKey] = SecondOrderBook[secondSymbolLowestAskKey] -  bottleneck.Value / FirstOrderBookVolumeConverter.Value / secondSymbolLowestAskKey; //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdOrderBook[thirdSymbolHighestBidKey] = ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondOrderBook.Remove(secondSymbolLowestAskKey, out var _);

                    FirstOrderBook[firstSymbolLowestAskKey] = FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade must be quoted in BTC terms
                    ThirdOrderBook[thirdSymbolHighestBidKey] = ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms

                }
                else 
                {
                    ThirdSymbolLayers++;
                    ThirdOrderBook.Remove(thirdSymbolHighestBidKey, out var _);

                    FirstOrderBook[firstSymbolLowestAskKey] = FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade is quoted in btc terms
                    SecondOrderBook[secondSymbolLowestAskKey] = SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / FirstOrderBookVolumeConverter.Value / secondSymbolLowestAskKey; //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                var secondSymbolHighestBidKey = SecondOrderBook.Keys.Max(); //this only needs to be defined within this scope
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstOrderBook.Remove(firstSymbolLowestAskKey, out var _);

                    SecondOrderBook[secondSymbolHighestBidKey] = SecondOrderBook[secondSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey / secondSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdOrderBook[thirdSymbolHighestBidKey] = ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms

                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondOrderBook.Remove(secondSymbolHighestBidKey, out var _);

                    FirstOrderBook[firstSymbolLowestAskKey] = FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade must be quoted in BTC terms
                    ThirdOrderBook[thirdSymbolHighestBidKey] = ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolLayers++;
                    ThirdOrderBook.Remove(thirdSymbolHighestBidKey, out var _);

                    FirstOrderBook[firstSymbolLowestAskKey] = FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade must be quoted in BTC terms
                    SecondOrderBook[secondSymbolHighestBidKey] = SecondOrderBook[secondSymbolHighestBidKey] - bottleneck.Value / ThirdOrderBookVolumeConverter.Key / secondSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else // sell buy sell
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstOrderBook.Remove(firstSymbolHighestBidKey, out var _); 
                    //second trade depth is expressed in altcoin terms. to convert to BTC, use the third orderbook bid price
                    SecondOrderBook[secondSymbolLowestAskKey] = SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / thirdSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdOrderBook[thirdSymbolHighestBidKey] = ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondOrderBook.Remove(secondSymbolLowestAskKey, out var _);

                    FirstOrderBook[firstSymbolHighestBidKey] = FirstOrderBook[firstSymbolHighestBidKey] - bottleneck.Value; //first trade depth is already in BTC terms
                    ThirdOrderBook[thirdSymbolHighestBidKey] = ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolLayers++;
                    ThirdOrderBook.Remove(thirdSymbolHighestBidKey, out var _);

                    FirstOrderBook[firstSymbolHighestBidKey] = FirstOrderBook[firstSymbolHighestBidKey] - bottleneck.Value; //first trade depth is already in BTC terms
                    SecondOrderBook[secondSymbolLowestAskKey] = SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / thirdSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using third orderbook.
                }
            }



        }

        private decimal GetProfitPercent() 
        {
             //use the direction list to understand what trades to make at each step
            if (Direction == Directions.BuySellSell)
            {
                var firstTrade = 1 / FirstOrderBook.Keys.Min();
                var secondTrade = firstTrade * SecondOrderBook.Keys.Max(); //sell
                var thirdTrade = secondTrade * ThirdOrderBook.Keys.Max(); //sell
                return thirdTrade - 1;
            }
            else if (Direction == Directions.BuyBuySell)
            {
                var firstTrade = 1 / FirstOrderBook.Keys.Min();
                var secondTrade = firstTrade / SecondOrderBook.Keys.Min(); //buy
                var thirdTrade = secondTrade * ThirdOrderBook.Keys.Max(); //sell
                return thirdTrade - 1;
            }
            else //Sell Buy Sell
            {
                var firstTrade = 1 * FirstOrderBook.Keys.Max();
                var secondTrade = firstTrade / SecondOrderBook.Keys.Min();
                var thirdTrade = secondTrade * ThirdOrderBook.Keys.Max();
                return thirdTrade - 1;
            }
        }

        private KeyValuePair<Bottlenecks, decimal> GetMaxVolume()
        {
            if (Direction == Directions.BuyBuySell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // the second trade is in the other base's terms, so you must convert the base-terms volume into BTC using the first trade price (which is base-BTC) 
                // Other than that, the logic is the same as the first trade since we are buying something again.

                decimal firstBtcVolume = FirstOrderBook.Keys.Min() * FirstOrderBook[FirstOrderBook.Keys.Min()]; 
                decimal secondBtcVolume = SecondOrderBook.Keys.Min() * SecondOrderBook[SecondOrderBook.Keys.Min()] * FirstOrderBookVolumeConverter.Key;
                // the third direction must be Sell at this point (there is no other potential combination)
                decimal thirdBtcVolume = ThirdOrderBook.Keys.Max() * ThirdOrderBook[ThirdOrderBook.Keys.Max()];
                //calculate and identify the bottleneck

                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.FirstTrade, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.SecondTrade, secondBtcVolume);
                }
                else
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.ThirdTrade, thirdBtcVolume);
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // second trade is a sell order, so the direction must be Buy Sell Sell
                // the depth is expressed in altcoin terms which must be converted to BTC. Price is expressed in basecoin terms.
                // the first order book contains the ALT-BTC price, which is therefore used to convert the volume to BTC terms
                decimal firstBtcVolume = FirstOrderBook.Keys.Min() * FirstOrderBook[FirstOrderBook.Keys.Min()];
                decimal secondBtcVolume = SecondOrderBook.Keys.Max() * SecondOrderBook[SecondOrderBook.Keys.Max()] * ThirdOrderBook.Keys.Max();
                // third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdOrderBook.Keys.Max() * ThirdOrderBook[ThirdOrderBook.Keys.Max()];
                //calculate and identify the bottleneck
                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.FirstTrade, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.SecondTrade, secondBtcVolume);
                }
                else
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.ThirdTrade, thirdBtcVolume);
                }
            }
            else //Sell Buy Sell
            {
                //first trade is a sell order. only one direction starts with a sell order: Sell Buy Sell
                //the only scenario when the first trade is a sell order is USDT/TUSD based trades, in which depth is already expressed in BTC (price is expressed in USD)
                decimal firstBtcVolume = FirstOrderBook[FirstOrderBook.Keys.Max()];
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = SecondOrderBook[SecondOrderBook.Keys.Min()] * ThirdOrderBook.Keys.Max();
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdOrderBook.Keys.Max() * ThirdOrderBook[ThirdOrderBook.Keys.Max()];
                //calculate and identify the bottleneck
                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.FirstTrade, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.SecondTrade, secondBtcVolume);
                }
                else
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.ThirdTrade, thirdBtcVolume);
                }
            }
        }
    }
}