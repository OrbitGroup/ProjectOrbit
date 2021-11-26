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

        public IOrderbook FirstSymbolOrderbook { get; set; }

        public Dictionary<decimal, decimal> FirstOrderBook { get; set; }
        public KeyValuePair<decimal, decimal> FirstOrderBookVolumeConverter { get; set; }
        public Dictionary<decimal, decimal> SecondOrderBook { get; set; }
        public Dictionary<decimal, decimal> ThirdOrderBook { get; set; }
        public KeyValuePair<decimal, decimal> ThirdOrderBookVolumeConverter { get; set; }

        public string SecondSymbol { get; set; }

        public IOrderbook SecondSymbolOrderbook { get; set; }

        public string ThirdSymbol { get; set; }

        public IOrderbook ThirdSymbolOrderbook { get; set; }

        public decimal ProfitPercent { get; set; }

        public decimal Profit { get; set; }

        public decimal MaxVolume { get; set; }

        public long VolumeComputeTime = 0;

        public int ProfitabilityComputeCount = 0;

        public long ProfitabilityComputeTime = 0;

        public long LiquidityRemovalComputeTime = 0;

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

        private readonly ILogger<Triangle> _logger;

        public Triangle(IOrderbook firstSymbolOrderbook, IOrderbook secondSymbolOrderbook, IOrderbook thirdSymbolOrderbook, Directions direction, IExchange exch)
        {
            FirstSymbolOrderbook = firstSymbolOrderbook;
            SecondSymbolOrderbook = secondSymbolOrderbook;
            ThirdSymbolOrderbook = thirdSymbolOrderbook;
            FirstSymbol = firstSymbolOrderbook.Symbol;
            SecondSymbol = secondSymbolOrderbook.Symbol;
            ThirdSymbol = thirdSymbolOrderbook.Symbol;
            Direction = direction;
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
                if (FirstOrderBook.Count == 0 || SecondOrderBook.Count == 0 || ThirdOrderBook.Count == 0)
                {
                    return false;
                } else
                {
                    return true;
                }
            }
        }

        public void SetMaxVolumeAndProfitability()
        {
            MaxVolume = 0;
            Profit = 0;
            var sw = new Stopwatch();

            while (NoEmptyOrderbooks)
            {
                sw.Start();
                var newProfitPercent = ProfitPercentCalculator.GetProfitPercent(this);
                sw.Stop();
                ProfitabilityComputeTime += sw.ElapsedMilliseconds;
                ProfitabilityComputeCount++;
                sw.Reset();

                var maxVol = new KeyValuePair<Bottlenecks, decimal>();

                if (newProfitPercent > 0)
                {
                    //there is no direct utility in calculating the MaxVolume unless an opportunity is profitable
                    sw.Start();
                    maxVol = MaxVolumeCalculator.GetMaxVolume(this);
                    sw.Stop();
                    VolumeComputeTime += sw.ElapsedMilliseconds;
                    sw.Reset();

                    sw.Start();
                    LiquidityRemover.RemoveLiquidity(maxVol, this);
                    sw.Stop();
                    LiquidityRemovalComputeTime += sw.ElapsedMilliseconds;
                    sw.Reset();
                    
                    MaxVolume += maxVol.Value;
                    Profit += maxVol.Value * newProfitPercent;
                    ProfitPercent = Profit / MaxVolume;
                    MapResultstoSymbols(); //log the symbols that have experienced positive profitability
                }
                else
                {
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
            //if the triangle is profitable, map each symbol to the ProfitableSymbolMapping dictionary along with the current time
            if (Profit > 0)
            {
                Exchange.ProfitableSymbolMapping[FirstSymbol] = DateTime.UtcNow;
                Exchange.ProfitableSymbolMapping[SecondSymbol] = DateTime.UtcNow;
                Exchange.ProfitableSymbolMapping[ThirdSymbol] = DateTime.UtcNow;
            } else
            {
                return;
            }
        }
    }
}