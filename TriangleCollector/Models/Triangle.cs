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
        public DateTime LastQueued = new DateTime(); //records the last time that this triangle was added to TrianglesToRecalculate

        public IExchange Exchange { get; set; }

        public string FirstSymbol { get; set; }
        public int FirstSymbolLayers { get; set; }

        public IOrderbook FirstSymbolOrderbook { get; set; }

        public string SecondSymbol { get; set; }
        public int SecondSymbolLayers { get; set; }

        public IOrderbook SecondSymbolOrderbook { get; set; }

        public string ThirdSymbol { get; set; }
        public int ThirdSymbolLayers { get; set; }

        public IOrderbook ThirdSymbolOrderbook { get; set; }

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
                if (ThirdSymbolOrderbook.OfficialBids.Count == 0) //the third trade is always a bid
                {
                    return false;
                }
                if (Direction == Directions.SellBuySell)
                {
                    return !(FirstSymbolOrderbook.OfficialBids.Count == 0 || SecondSymbolOrderbook.OfficialAsks.Count == 0);
                }
                else if (Direction == Directions.BuyBuySell)
                {
                    return !(FirstSymbolOrderbook.OfficialAsks.Count == 0 || SecondSymbolOrderbook.OfficialAsks.Count == 0);
                } 
                else if (Direction == Directions.BuySellSell)
                {
                    return !(FirstSymbolOrderbook.OfficialAsks.Count == 0 || SecondSymbolOrderbook.OfficialBids.Count == 0);
                } else
                {
                    return false;
                }
            }
        }

        public bool SetMaxVolumeAndProfitability(IOrderbook firstSymbolOrderbook, IOrderbook secondSymbolOrderbook, IOrderbook thirdSymbolOrderbook)
        {
            try 
            {
                bool firstOrderbookEntered = Monitor.TryEnter(firstSymbolOrderbook.OrderbookLock, TimeSpan.FromMilliseconds(2));
                if (!firstOrderbookEntered)
                {
                    return false;
                }

                bool secondOrderbookEntered = Monitor.TryEnter(secondSymbolOrderbook.OrderbookLock, TimeSpan.FromMilliseconds(2));
                if (!secondOrderbookEntered)
                {
                    return false;
                }

                bool thirdOrderbookEntered = Monitor.TryEnter(thirdSymbolOrderbook.OrderbookLock, TimeSpan.FromMilliseconds(2));
                if (!thirdOrderbookEntered)
                {
                    return false;
                }
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                FirstSymbolOrderbook = firstSymbolOrderbook.DeepCopy();
                SecondSymbolOrderbook = secondSymbolOrderbook.DeepCopy();
                ThirdSymbolOrderbook = thirdSymbolOrderbook.DeepCopy();
                stopwatch.Stop();
                //_logger.LogDebug($"time to DeepCopy: {stopwatch.ElapsedMilliseconds} milliseconds");
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
                if (maxVol.Value == 0)
                {
                    _logger.LogError("Threading Issue: MaxVol is Zero");
                }

                if (newProfitPercent > 0)
                {
                    RemoveLiquidity(maxVol);
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
            if (Direction == Directions.BuyBuySell)
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstSymbolOrderbook.OfficialAsks.TryRemove(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), out var _);

                    SecondSymbolOrderbook.OfficialAsks[SecondSymbolOrderbook.OfficialAsks.Keys.Min()] = SecondSymbolOrderbook.OfficialAsks.GetOrAdd(SecondSymbolOrderbook.OfficialAsks.Keys.Min(), 0) -  bottleneck.Value / FirstSymbolOrderbook.OfficialBids.GetOrAdd(FirstSymbolOrderbook.OfficialBids.Keys.Max(), 0) / SecondSymbolOrderbook.OfficialAsks.Keys.Min(); //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.OfficialBids[ThirdSymbolOrderbook.OfficialBids.Keys.Max()] = ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondSymbolOrderbook.OfficialAsks.TryRemove(SecondSymbolOrderbook.OfficialAsks.Keys.Min(), out var _);

                    FirstSymbolOrderbook.OfficialAsks[FirstSymbolOrderbook.OfficialAsks.Keys.Min()] = FirstSymbolOrderbook.OfficialAsks.GetOrAdd(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.OfficialAsks.Keys.Min(); //first trade must be quoted in BTC terms
                    ThirdSymbolOrderbook.OfficialBids[ThirdSymbolOrderbook.OfficialBids.Keys.Max()] = ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //last trade must be quoted in BTC terms

                }
                else
                {
                    ThirdSymbolLayers++;
                    ThirdSymbolOrderbook.OfficialBids.TryRemove(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.OfficialAsks[FirstSymbolOrderbook.OfficialAsks.Keys.Min()] = FirstSymbolOrderbook.OfficialAsks.GetOrAdd(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.OfficialAsks.Keys.Min(); //first trade is quoted in btc terms
                    SecondSymbolOrderbook.OfficialAsks[SecondSymbolOrderbook.OfficialAsks.Keys.Min()] = SecondSymbolOrderbook.OfficialAsks.GetOrAdd(SecondSymbolOrderbook.OfficialAsks.Keys.Max(), 0) - bottleneck.Value / FirstSymbolOrderbook.OfficialBids.GetOrAdd(FirstSymbolOrderbook.OfficialBids.Keys.Max(), 0) / SecondSymbolOrderbook.OfficialAsks.Keys.Min(); //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstSymbolOrderbook.OfficialAsks.TryRemove(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), out var _);

                    SecondSymbolOrderbook.OfficialBids[SecondSymbolOrderbook.OfficialBids.Keys.Max()] = SecondSymbolOrderbook.OfficialBids.GetOrAdd(SecondSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max() / SecondSymbolOrderbook.OfficialBids.Keys.Max(); //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.OfficialBids[ThirdSymbolOrderbook.OfficialBids.Keys.Max()] = ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //last trade must be quoted in BTC terms

                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondSymbolOrderbook.OfficialBids.TryRemove(SecondSymbolOrderbook.OfficialBids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.OfficialAsks[FirstSymbolOrderbook.OfficialAsks.Keys.Min()] = FirstSymbolOrderbook.OfficialAsks.GetOrAdd(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.OfficialAsks.Keys.Min(); //first trade must be quoted in BTC terms
                    ThirdSymbolOrderbook.OfficialBids[ThirdSymbolOrderbook.OfficialBids.Keys.Max()] = ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolLayers++;
                    ThirdSymbolOrderbook.OfficialBids.TryRemove(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.OfficialAsks[FirstSymbolOrderbook.OfficialAsks.Keys.Min()] = FirstSymbolOrderbook.OfficialAsks.GetOrAdd(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.OfficialAsks.Keys.Min(); //first trade must be quoted in BTC terms
                    SecondSymbolOrderbook.OfficialBids[SecondSymbolOrderbook.OfficialBids.Keys.Max()] = SecondSymbolOrderbook.OfficialBids.GetOrAdd(SecondSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialAsks.Keys.Min() / SecondSymbolOrderbook.OfficialBids.Keys.Max(); //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else // sell buy sell
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstSymbolOrderbook.OfficialBids.TryRemove(FirstSymbolOrderbook.OfficialBids.Keys.Max(), out var _); 
                    //second trade depth is expressed in altcoin terms. to convert to BTC, use the third orderbook bid price
                    SecondSymbolOrderbook.OfficialAsks[SecondSymbolOrderbook.OfficialAsks.Keys.Min()] = SecondSymbolOrderbook.OfficialAsks.GetOrAdd(SecondSymbolOrderbook.OfficialAsks.Keys.Min(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.OfficialBids[ThirdSymbolOrderbook.OfficialBids.Keys.Max()] = ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondSymbolOrderbook.OfficialAsks.TryRemove(SecondSymbolOrderbook.OfficialAsks.Keys.Min(), out var _);

                    FirstSymbolOrderbook.OfficialBids[FirstSymbolOrderbook.OfficialBids.Keys.Max()] = FirstSymbolOrderbook.OfficialBids.GetOrAdd(FirstSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value; //first trade depth is already in BTC terms
                    ThirdSymbolOrderbook.OfficialBids[ThirdSymbolOrderbook.OfficialBids.Keys.Max()] = ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolLayers++;
                    ThirdSymbolOrderbook.OfficialBids.TryRemove(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.OfficialBids[FirstSymbolOrderbook.OfficialBids.Keys.Max()] = FirstSymbolOrderbook.OfficialBids.GetOrAdd(FirstSymbolOrderbook.OfficialBids.Keys.Max(), 0) - bottleneck.Value; //first trade depth is already in BTC terms
                    SecondSymbolOrderbook.OfficialAsks[SecondSymbolOrderbook.OfficialAsks.Keys.Min()] = SecondSymbolOrderbook.OfficialAsks.GetOrAdd(SecondSymbolOrderbook.OfficialAsks.Keys.Min(), 0) - bottleneck.Value / ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //second trade is quoted in alt terms, so convert using third orderbook.
                }
            }



        }

        private decimal GetProfitPercent() 
        {
             //use the direction list to understand what trades to make at each step
            if (Direction == Directions.BuySellSell)
            {
                var firstTrade = 1 / FirstSymbolOrderbook.OfficialAsks.Keys.Min();
                var secondTrade = firstTrade * SecondSymbolOrderbook.OfficialBids.Keys.Max(); //sell
                var thirdTrade = secondTrade * ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //sell
                return thirdTrade - 1;
            }
            else if (Direction == Directions.BuyBuySell)
            {
                var firstTrade = 1 / FirstSymbolOrderbook.OfficialAsks.Keys.Min();
                var secondTrade = firstTrade / SecondSymbolOrderbook.OfficialAsks.Keys.Min(); //buy
                var thirdTrade = secondTrade * ThirdSymbolOrderbook.OfficialBids.Keys.Max(); //sell
                return thirdTrade - 1;
            }
            else //Sell Buy Sell
            {
                var firstTrade = 1 * FirstSymbolOrderbook.OfficialBids.Keys.Max();
                var secondTrade = firstTrade / SecondSymbolOrderbook.OfficialAsks.Keys.Min();
                var thirdTrade = secondTrade * ThirdSymbolOrderbook.OfficialBids.Keys.Max();
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

                decimal firstBtcVolume = FirstSymbolOrderbook.OfficialAsks.Keys.Min() * FirstSymbolOrderbook.OfficialAsks.GetOrAdd(FirstSymbolOrderbook.OfficialAsks.Keys.Min(),0); 
                decimal secondBtcVolume = SecondSymbolOrderbook.OfficialAsks.Keys.Min() * SecondSymbolOrderbook.OfficialAsks.GetOrAdd(SecondSymbolOrderbook.OfficialAsks.Keys.Min(), 0) * FirstSymbolOrderbook.OfficialBids.Keys.Max();
                // the third direction must be Sell at this point (there is no other potential combination)
                decimal thirdBtcVolume = ThirdSymbolOrderbook.OfficialBids.Keys.Max() * ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(),0);
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
                decimal firstBtcVolume = FirstSymbolOrderbook.OfficialAsks.Keys.Min() * FirstSymbolOrderbook.OfficialAsks.GetOrAdd(FirstSymbolOrderbook.OfficialAsks.Keys.Min(), 0);
                decimal secondBtcVolume = SecondSymbolOrderbook.OfficialBids.Keys.Max() * SecondSymbolOrderbook.OfficialBids.GetOrAdd(SecondSymbolOrderbook.OfficialBids.Keys.Max(),0) * ThirdSymbolOrderbook.OfficialBids.Keys.Max();
                // third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.OfficialBids.Keys.Max() * ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0);
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
                decimal firstBtcVolume = FirstSymbolOrderbook.OfficialBids.GetOrAdd(FirstSymbolOrderbook.OfficialBids.Keys.Max(), 0);
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = SecondSymbolOrderbook.OfficialAsks.GetOrAdd(SecondSymbolOrderbook.OfficialAsks.Keys.Min(),0) * ThirdSymbolOrderbook.OfficialBids.Keys.Max();
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.OfficialBids.Keys.Max() * ThirdSymbolOrderbook.OfficialBids.GetOrAdd(ThirdSymbolOrderbook.OfficialBids.Keys.Max(), 0);
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