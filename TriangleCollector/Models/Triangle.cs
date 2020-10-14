using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public class Triangle
    {
        public string FirstSymbol { get; set; }
        public int FirstSymbolLayers { get; set; }

        public Orderbook FirstSymbolOrderbook { get; set; }

        public string SecondSymbol { get; set; }
        public int SecondSymbolLayers { get; set; }

        public Orderbook SecondSymbolOrderbook { get; set; }

        public string ThirdSymbol { get; set; }
        public int ThirdSymbolLayers { get; set; }

        public Orderbook ThirdSymbolOrderbook { get; set; }

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

        public bool NoEmptyOrderbooks //Why does it only encompass one direction?
        {
            get
            {
                if (Direction == Directions.SellBuySell)
                {
                    return !(FirstSymbolOrderbook.bids.Count == 0 || SecondSymbolOrderbook.asks.Count == 0 || ThirdSymbolOrderbook.bids.Count == 0);
                }
                else
                {
                    return true;
                }
            }
        }

        public bool SetMaxVolumeAndProfitability(Orderbook firstSymbolOrderbook, Orderbook secondSymbolOrderbook, Orderbook thirdSymbolOrderbook)
        {
            try 
            {
                bool firstOrderbookEntered = Monitor.TryEnter(firstSymbolOrderbook.orderbookLock, TimeSpan.FromMilliseconds(2));
                if (!firstOrderbookEntered)
                {
                    return false;
                }

                bool secondOrderbookEntered = Monitor.TryEnter(secondSymbolOrderbook.orderbookLock, TimeSpan.FromMilliseconds(2));
                if (!secondOrderbookEntered)
                {
                    return false;
                }

                bool thirdOrderbookEntered = Monitor.TryEnter(thirdSymbolOrderbook.orderbookLock, TimeSpan.FromMilliseconds(2));
                if (!thirdOrderbookEntered)
                {
                    return false;
                }

                FirstSymbolOrderbook = firstSymbolOrderbook;
                SecondSymbolOrderbook = secondSymbolOrderbook;
                ThirdSymbolOrderbook = thirdSymbolOrderbook;

                SetMaxVolumeAndProfitability();
            }
            finally
            {
                if (Monitor.IsEntered(firstSymbolOrderbook.orderbookLock)) Monitor.Exit(firstSymbolOrderbook.orderbookLock);
                if (Monitor.IsEntered(secondSymbolOrderbook.orderbookLock)) Monitor.Exit(secondSymbolOrderbook.orderbookLock);
                if (Monitor.IsEntered(thirdSymbolOrderbook.orderbookLock)) Monitor.Exit(thirdSymbolOrderbook.orderbookLock);
            }

            return true;
        }

        public void SetMaxVolumeAndProfitability()
        {
            
            MaxVolume = 0;
            Profit = 0;
            FirstSymbolLayers = 1;
            SecondSymbolLayers = 1;
            ThirdSymbolLayers = 1;

            while (NoEmptyOrderbooks)
            {
                var newProfitPercent = GetProfitPercent();
                if (newProfitPercent == -2)
                {
                    return;
                }

                var maxVol = GetMaxVolume();

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
                TriangleCollector.ProfitableSymbolMapping.AddOrUpdate(FirstSymbol, FirstSymbolLayers, (key, oldValue) => Math.Max(oldValue, FirstSymbolLayers));
                TriangleCollector.ProfitableSymbolMapping.AddOrUpdate(SecondSymbol, SecondSymbolLayers, (key, oldValue) => Math.Max(oldValue, SecondSymbolLayers));
                TriangleCollector.ProfitableSymbolMapping.AddOrUpdate(ThirdSymbol, ThirdSymbolLayers, (key, oldValue) => Math.Max(oldValue, ThirdSymbolLayers));
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
                    FirstSymbolOrderbook.asks.TryRemove(FirstSymbolOrderbook.asks.Keys.Min(), out var _);

                    SecondSymbolOrderbook.asks[SecondSymbolOrderbook.asks.Keys.Min()] = SecondSymbolOrderbook.asks.GetOrAdd(SecondSymbolOrderbook.asks.Keys.Min(), 0) -  bottleneck.Value / FirstSymbolOrderbook.bids.GetOrAdd(FirstSymbolOrderbook.bids.Keys.Max(), 0) / SecondSymbolOrderbook.asks.Keys.Min(); //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.bids[ThirdSymbolOrderbook.bids.Keys.Max()] = ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondSymbolOrderbook.asks.TryRemove(SecondSymbolOrderbook.asks.Keys.Min(), out var _);

                    FirstSymbolOrderbook.asks[FirstSymbolOrderbook.asks.Keys.Min()] = FirstSymbolOrderbook.asks.GetOrAdd(FirstSymbolOrderbook.asks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.asks.Keys.Min(); //first trade must be quoted in BTC terms
                    ThirdSymbolOrderbook.bids[ThirdSymbolOrderbook.bids.Keys.Max()] = ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //last trade must be quoted in BTC terms

                }
                else
                {
                    ThirdSymbolLayers++;
                    ThirdSymbolOrderbook.bids.TryRemove(ThirdSymbolOrderbook.bids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.asks[FirstSymbolOrderbook.asks.Keys.Min()] = FirstSymbolOrderbook.asks.GetOrAdd(FirstSymbolOrderbook.asks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.asks.Keys.Min(); //first trade is quoted in btc terms
                    SecondSymbolOrderbook.asks[SecondSymbolOrderbook.asks.Keys.Min()] = SecondSymbolOrderbook.asks.GetOrAdd(SecondSymbolOrderbook.asks.Keys.Max(), 0) - bottleneck.Value / FirstSymbolOrderbook.bids.GetOrAdd(FirstSymbolOrderbook.bids.Keys.Max(), 0) / SecondSymbolOrderbook.asks.Keys.Min(); //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstSymbolOrderbook.asks.TryRemove(FirstSymbolOrderbook.asks.Keys.Min(), out var _);

                    SecondSymbolOrderbook.bids[SecondSymbolOrderbook.bids.Keys.Max()] = SecondSymbolOrderbook.bids.GetOrAdd(SecondSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max() / SecondSymbolOrderbook.bids.Keys.Max(); //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.bids[ThirdSymbolOrderbook.bids.Keys.Max()] = ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //last trade must be quoted in BTC terms

                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondSymbolOrderbook.bids.TryRemove(SecondSymbolOrderbook.bids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.asks[FirstSymbolOrderbook.asks.Keys.Min()] = FirstSymbolOrderbook.asks.GetOrAdd(FirstSymbolOrderbook.asks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.asks.Keys.Min(); //first trade must be quoted in BTC terms
                    ThirdSymbolOrderbook.bids[ThirdSymbolOrderbook.bids.Keys.Max()] = ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolLayers++;
                    ThirdSymbolOrderbook.bids.TryRemove(ThirdSymbolOrderbook.bids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.asks[FirstSymbolOrderbook.asks.Keys.Min()] = FirstSymbolOrderbook.asks.GetOrAdd(FirstSymbolOrderbook.asks.Keys.Min(), 0) - bottleneck.Value / FirstSymbolOrderbook.asks.Keys.Min(); //first trade must be quoted in BTC terms
                    SecondSymbolOrderbook.bids[SecondSymbolOrderbook.bids.Keys.Max()] = SecondSymbolOrderbook.bids.GetOrAdd(SecondSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.asks.Keys.Min() / SecondSymbolOrderbook.bids.Keys.Max(); //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else // sell buy sell
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolLayers++;
                    FirstSymbolOrderbook.bids.TryRemove(FirstSymbolOrderbook.bids.Keys.Max(), out var _); 
                    //second trade depth is expressed in altcoin terms. to convert to BTC, use the third orderbook bid price
                    SecondSymbolOrderbook.asks[SecondSymbolOrderbook.asks.Keys.Min()] = SecondSymbolOrderbook.asks.GetOrAdd(SecondSymbolOrderbook.asks.Keys.Min(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.bids[ThirdSymbolOrderbook.bids.Keys.Max()] = ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolLayers++;
                    SecondSymbolOrderbook.asks.TryRemove(SecondSymbolOrderbook.asks.Keys.Min(), out var _);

                    FirstSymbolOrderbook.bids[FirstSymbolOrderbook.bids.Keys.Max()] = FirstSymbolOrderbook.bids.GetOrAdd(FirstSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value; //first trade depth is already in BTC terms
                    ThirdSymbolOrderbook.bids[ThirdSymbolOrderbook.bids.Keys.Max()] = ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolLayers++;
                    ThirdSymbolOrderbook.bids.TryRemove(ThirdSymbolOrderbook.bids.Keys.Max(), out var _);

                    FirstSymbolOrderbook.bids[FirstSymbolOrderbook.bids.Keys.Max()] = FirstSymbolOrderbook.bids.GetOrAdd(FirstSymbolOrderbook.bids.Keys.Max(), 0) - bottleneck.Value; //first trade depth is already in BTC terms
                    SecondSymbolOrderbook.asks[SecondSymbolOrderbook.asks.Keys.Min()] = SecondSymbolOrderbook.asks.GetOrAdd(SecondSymbolOrderbook.asks.Keys.Min(), 0) - bottleneck.Value / ThirdSymbolOrderbook.bids.Keys.Max(); //second trade is quoted in alt terms, so convert using third orderbook.
                }
            }



        }

        private decimal GetProfitPercent() 
        {
            try //use the direction list to understand what trades to make at each step
            {
                if (Direction == Directions.BuySellSell)
                {
                    var firstTrade = 1 / FirstSymbolOrderbook.asks.Keys.Min();
                    var secondTrade = firstTrade * SecondSymbolOrderbook.bids.Keys.Max(); //sell
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.bids.Keys.Max(); //sell
                    return thirdTrade - 1;
                }
                else if (Direction == Directions.BuyBuySell)
                {
                    var firstTrade = 1 / FirstSymbolOrderbook.asks.Keys.Min();
                    var secondTrade = firstTrade / SecondSymbolOrderbook.asks.Keys.Min(); //buy
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.bids.Keys.Max(); //sell
                    return thirdTrade - 1;
                }
                else //Sell Buy Sell
                {
                    var firstTrade = 1 * FirstSymbolOrderbook.bids.Keys.Max();
                    var secondTrade = firstTrade / SecondSymbolOrderbook.asks.Keys.Min();
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.bids.Keys.Max();
                    return thirdTrade - 1;
                }
            }
            catch (Exception ex)
            {
                return -2;
            }
        }

        private KeyValuePair<Bottlenecks, decimal> GetMaxVolume()
        {
            if (Direction == Directions.BuyBuySell)
            {
                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // the second trade is in the other base's terms, so you must convert the base-terms volume into BTC using the first trade price (which is base-BTC) 
                // Other than that, the logic is the same as the first trade since we are buying something again.

                decimal firstBtcVolume = FirstSymbolOrderbook.asks.Keys.Min() * FirstSymbolOrderbook.asks.GetOrAdd(FirstSymbolOrderbook.asks.Keys.Min(),0); 
                decimal secondBtcVolume = SecondSymbolOrderbook.asks.Keys.Min() * SecondSymbolOrderbook.asks.GetOrAdd(SecondSymbolOrderbook.asks.Keys.Min(), 0) * FirstSymbolOrderbook.bids.Keys.Max();
                // the third direction must be Sell at this point (there is no other potential combination)
                decimal thirdBtcVolume = ThirdSymbolOrderbook.bids.Keys.Max() * ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(),0);
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
                decimal firstBtcVolume = FirstSymbolOrderbook.asks.Keys.Min() * FirstSymbolOrderbook.asks.GetOrAdd(FirstSymbolOrderbook.asks.Keys.Min(), 0);
                decimal secondBtcVolume = SecondSymbolOrderbook.bids.Keys.Max() * SecondSymbolOrderbook.bids.GetOrAdd(SecondSymbolOrderbook.bids.Keys.Max(),0) * ThirdSymbolOrderbook.bids.Keys.Max();
                // third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.bids.Keys.Max() * ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0);
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
                decimal firstBtcVolume = FirstSymbolOrderbook.bids.GetOrAdd(FirstSymbolOrderbook.bids.Keys.Max(), 0);
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = SecondSymbolOrderbook.asks.GetOrAdd(SecondSymbolOrderbook.asks.Keys.Min(),0) * ThirdSymbolOrderbook.bids.Keys.Max();
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.bids.Keys.Max() * ThirdSymbolOrderbook.bids.GetOrAdd(ThirdSymbolOrderbook.bids.Keys.Max(), 0);
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