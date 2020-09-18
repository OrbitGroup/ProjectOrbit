using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public class Triangle
    {
        public string FirstSymbol { get; set; }

        public Orderbook FirstSymbolOrderbook { get; set; }

        public string SecondSymbol { get; set; }

        public Orderbook SecondSymbolOrderbook { get; set; }

        public string ThirdSymbol { get; set; }

        public Orderbook ThirdSymbolOrderbook { get; set; }

        public decimal ProfitPercent { get; set; }

        public decimal Profit { get; set; }

        public decimal MaxVolume { get; set; }

        public int Layers { get; set; }

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

        public bool NoEmptyOrderbooks
        {
            get
            {
                if (Direction == Directions.SellBuySell)
                {
                    return !(FirstSymbolOrderbook.SortedBids.Count == 0 || SecondSymbolOrderbook.SortedAsks.Count == 0 || ThirdSymbolOrderbook.SortedBids.Count == 0);
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
                FirstSymbolOrderbook.CreateSorted();
                SecondSymbolOrderbook.CreateSorted();
                ThirdSymbolOrderbook.CreateSorted();

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
            Layers = 0; 
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
                    Layers++;
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
            if (Profit > 0)
            {
                TriangleCollector.ProfitableSymbolMapping.AddOrUpdate(FirstSymbol, Layers, (key, oldValue) => oldValue = Layers);
                TriangleCollector.ProfitableSymbolMapping.AddOrUpdate(SecondSymbol, Layers, (key, oldValue) => oldValue = Layers);
                TriangleCollector.ProfitableSymbolMapping.AddOrUpdate(ThirdSymbol, Layers, (key, oldValue) => oldValue = Layers);
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
                    FirstSymbolOrderbook.SortedAsks.Remove(FirstSymbolOrderbook.SortedAsks.First().Key);

                    SecondSymbolOrderbook.SortedAsks[SecondSymbolOrderbook.SortedAsks.First().Key] = SecondSymbolOrderbook.SortedAsks.First().Value -  bottleneck.Value / FirstSymbolOrderbook.SortedBids.First().Value / SecondSymbolOrderbook.SortedAsks.First().Key; //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.SortedBids[ThirdSymbolOrderbook.SortedBids.First().Key] = ThirdSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolOrderbook.SortedAsks.Remove(SecondSymbolOrderbook.SortedAsks.First().Key);

                    FirstSymbolOrderbook.SortedAsks[FirstSymbolOrderbook.SortedAsks.First().Key] = FirstSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / FirstSymbolOrderbook.SortedAsks.First().Key; //first trade must be quoted in BTC terms
                    ThirdSymbolOrderbook.SortedBids[ThirdSymbolOrderbook.SortedBids.First().Key] = ThirdSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //last trade must be quoted in BTC terms

                }
                else
                {
                    ThirdSymbolOrderbook.SortedBids.Remove(ThirdSymbolOrderbook.SortedBids.First().Key);

                    FirstSymbolOrderbook.SortedAsks[FirstSymbolOrderbook.SortedAsks.First().Key] = FirstSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / FirstSymbolOrderbook.SortedAsks.First().Key; //first trade is quoted in btc terms
                    SecondSymbolOrderbook.SortedAsks[SecondSymbolOrderbook.SortedAsks.First().Key] = SecondSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / FirstSymbolOrderbook.SortedBids.First().Value / SecondSymbolOrderbook.SortedAsks.First().Key; //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else if (Direction == Directions.BuySellSell)
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolOrderbook.SortedAsks.Remove(FirstSymbolOrderbook.SortedAsks.First().Key);

                    SecondSymbolOrderbook.SortedBids[SecondSymbolOrderbook.SortedBids.First().Key] = SecondSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key / SecondSymbolOrderbook.SortedBids.First().Key; //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.SortedBids[ThirdSymbolOrderbook.SortedBids.First().Key] = ThirdSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //last trade must be quoted in BTC terms

                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolOrderbook.SortedBids.Remove(SecondSymbolOrderbook.SortedBids.First().Key);

                    FirstSymbolOrderbook.SortedAsks[FirstSymbolOrderbook.SortedAsks.First().Key] = FirstSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / FirstSymbolOrderbook.SortedAsks.First().Key; //first trade must be quoted in BTC terms
                    ThirdSymbolOrderbook.SortedBids[ThirdSymbolOrderbook.SortedBids.First().Key] = ThirdSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolOrderbook.SortedBids.Remove(ThirdSymbolOrderbook.SortedBids.First().Key);

                    FirstSymbolOrderbook.SortedAsks[FirstSymbolOrderbook.SortedAsks.First().Key] = FirstSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / FirstSymbolOrderbook.SortedAsks.First().Key; //first trade must be quoted in BTC terms
                    SecondSymbolOrderbook.SortedBids[SecondSymbolOrderbook.SortedBids.First().Key] = SecondSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedAsks.First().Key / SecondSymbolOrderbook.SortedBids.First().Key; //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else // sell buy sell
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    FirstSymbolOrderbook.SortedBids.Remove(FirstSymbolOrderbook.SortedBids.First().Key); 
                    //second trade depth is expressed in altcoin terms. to convert to BTC, use the third orderbook bid price
                    SecondSymbolOrderbook.SortedAsks[SecondSymbolOrderbook.SortedAsks.First().Key] = SecondSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //second trade is quoted in alt terms, so convert using first orderbook.
                    ThirdSymbolOrderbook.SortedBids[ThirdSymbolOrderbook.SortedBids.First().Key] = ThirdSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    SecondSymbolOrderbook.SortedAsks.Remove(SecondSymbolOrderbook.SortedAsks.First().Key);

                    FirstSymbolOrderbook.SortedBids[FirstSymbolOrderbook.SortedBids.First().Key] = FirstSymbolOrderbook.SortedBids.First().Value - bottleneck.Value; //first trade depth is already in BTC terms
                    ThirdSymbolOrderbook.SortedBids[ThirdSymbolOrderbook.SortedBids.First().Key] = ThirdSymbolOrderbook.SortedBids.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    ThirdSymbolOrderbook.SortedBids.Remove(ThirdSymbolOrderbook.SortedBids.First().Key);

                    FirstSymbolOrderbook.SortedBids[FirstSymbolOrderbook.SortedBids.First().Key] = FirstSymbolOrderbook.SortedBids.First().Value - bottleneck.Value; //first trade depth is already in BTC terms
                    SecondSymbolOrderbook.SortedAsks[SecondSymbolOrderbook.SortedAsks.First().Key] = SecondSymbolOrderbook.SortedAsks.First().Value - bottleneck.Value / ThirdSymbolOrderbook.SortedBids.First().Key; //second trade is quoted in alt terms, so convert using third orderbook.
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
                    return thirdTrade - 1;
                }
                else if (Direction == Directions.BuyBuySell)
                {
                    var firstTrade = 1 / FirstSymbolOrderbook.SortedAsks.First().Key;
                    var secondTrade = firstTrade / SecondSymbolOrderbook.SortedAsks.First().Key; //buy
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key; //sell
                    return thirdTrade - 1;
                }
                else //Sell Buy Sell
                {
                    var firstTrade = 1 * FirstSymbolOrderbook.SortedBids.First().Key;
                    var secondTrade = firstTrade / SecondSymbolOrderbook.SortedAsks.First().Key;
                    var thirdTrade = secondTrade * ThirdSymbolOrderbook.SortedBids.First().Key;
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
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedAsks.First().Key * FirstSymbolOrderbook.SortedAsks.First().Value;
                decimal secondBtcVolume = SecondSymbolOrderbook.SortedAsks.First().Key * SecondSymbolOrderbook.SortedAsks.First().Value * FirstSymbolOrderbook.SortedBids.First().Key;
                // the third direction must be Sell at this point (there is no other potential combination)
                decimal thirdBtcVolume = ThirdSymbolOrderbook.SortedBids.First().Key * ThirdSymbolOrderbook.SortedBids.First().Value;
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
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedAsks.First().Key * FirstSymbolOrderbook.SortedAsks.First().Value;
                decimal secondBtcVolume = SecondSymbolOrderbook.SortedBids.First().Key * SecondSymbolOrderbook.SortedBids.First().Value * ThirdSymbolOrderbook.SortedBids.First().Key;
                // third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.SortedBids.First().Key * ThirdSymbolOrderbook.SortedBids.First().Value;
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
                decimal firstBtcVolume = FirstSymbolOrderbook.SortedBids.First().Value;
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = SecondSymbolOrderbook.SortedAsks.First().Value * ThirdSymbolOrderbook.SortedBids.First().Key;
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = ThirdSymbolOrderbook.SortedBids.First().Key * ThirdSymbolOrderbook.SortedBids.First().Value;
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