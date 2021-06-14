using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TriangleCollector.Models.Exchanges.Binance;
using TriangleCollector.Models.Interfaces;
using System.Collections.Concurrent;

namespace TriangleCollector.UnitTests
{
    [TestClass]
    public class BinanceMergeTests
    {
        [TestMethod]
        public void TestBinanceOrderbookDeserializer()
        {
            //Arrange: 
            //sample websocket message from Binance for an orderbook refresh:
            string sampleWebsocketMessage = "{\"u\":494630706,\"s\":\"XRPETH\",\"b\":\"0.00034050\",\"B\":\"131.00000000\",\"a\":\"0.00034350\",\"A\":\"1358.00000000\"}";
            //expected deserialized orderbook:
            var expectedOrderbook = new BinanceOrderbook();
            expectedOrderbook.Sequence = 494630706;
            expectedOrderbook.Symbol = "XRPETH";

            expectedOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            expectedOrderbook.OfficialBids.TryAdd(0.00034050m, 131m);

            expectedOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            expectedOrderbook.OfficialAsks.TryAdd(0.00034350m, 1358m);
            

            //Act: run the sample websocket message through the Huobi OrderbookConverter
            IOrderbook testOrderbook = (IOrderbook)JsonSerializer.Deserialize(sampleWebsocketMessage, typeof(BinanceOrderbook));

            //Assert: test that the actual outcome matches the expected outcome
            Assert.AreEqual(expectedOrderbook.Symbol, testOrderbook.Symbol);
            Assert.AreEqual(expectedOrderbook.Sequence, testOrderbook.Sequence);
            Assert.IsTrue(expectedOrderbook.OfficialAsks.Count == testOrderbook.OfficialAsks.Count &&
                expectedOrderbook.OfficialBids.Count == testOrderbook.OfficialBids.Count);
            
            Assert.IsTrue(CheckLayers(expectedOrderbook, testOrderbook));
        }
        [TestMethod]
        public void TestBinanceOrderbookMerge()
        {
            //Arrange
            //Beginning Orderbooks: one empty, one with 10 values, one with 5 values
            var dummyExchange = new BinanceExchange("binance");
            dummyExchange.ProfitableSymbolMapping.TryAdd("dummy", DateTime.UtcNow);

            var beginningEmptyOrderbook = new BinanceOrderbook();
            

            var beginningFullOrderbook = new BinanceOrderbook();
            beginningFullOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            beginningFullOrderbook.OfficialBids.TryAdd(32115.28m, 0.02m);


            beginningFullOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            beginningFullOrderbook.OfficialAsks.TryAdd(32115.29m, 0.005m);

            beginningEmptyOrderbook.Symbol = "XRPETH";
            beginningFullOrderbook.Symbol = "XRPETH";

            beginningEmptyOrderbook.Sequence = 1;
            //beginningHalfFullOrderbook.Sequence = 1;
            beginningFullOrderbook.Sequence = 1;

            beginningEmptyOrderbook.Exchange = dummyExchange;
            //beginningHalfFullOrderbook.Exchange = dummyExchange;
            beginningFullOrderbook.Exchange = dummyExchange;

            //arrange orderbook updates: one empty, one full, one with 6 values
            var updateFullOrderbook = new BinanceOrderbook();
            updateFullOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            updateFullOrderbook.OfficialBids.TryAdd(1m, 0.02m);
            updateFullOrderbook.OfficialBids.TryAdd(2m, 0.15m);
            updateFullOrderbook.OfficialBids.TryAdd(3m, 0.072382m);
            updateFullOrderbook.OfficialBids.TryAdd(4m, 0.066716m);
            updateFullOrderbook.OfficialBids.TryAdd(5m, 0.02m);
            updateFullOrderbook.OfficialBids.TryAdd(6m, 2.521393m);
            updateFullOrderbook.OfficialBids.TryAdd(7m, 0.117404m);
            updateFullOrderbook.OfficialBids.TryAdd(8m, 0.139009m);
            updateFullOrderbook.OfficialBids.TryAdd(9m, 0.123m);
            updateFullOrderbook.OfficialBids.TryAdd(10m, 0.11175m);

            updateFullOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            updateFullOrderbook.OfficialAsks.TryAdd(11m, 0.005m);
            updateFullOrderbook.OfficialAsks.TryAdd(12m, 0.03m);
            updateFullOrderbook.OfficialAsks.TryAdd(13m, 0.01759m);
            updateFullOrderbook.OfficialAsks.TryAdd(14m, 0.03m);
            updateFullOrderbook.OfficialAsks.TryAdd(15m, 0.03m);
            updateFullOrderbook.OfficialAsks.TryAdd(16m, 0.12m);
            updateFullOrderbook.OfficialAsks.TryAdd(17m, 0.08m);
            updateFullOrderbook.OfficialAsks.TryAdd(18m, 0.125775m);
            updateFullOrderbook.OfficialAsks.TryAdd(19m, 0.03563m);
            updateFullOrderbook.OfficialAsks.TryAdd(20m, 0.001m);

            var updateHalfFullOrderbook = new BinanceOrderbook();
            updateHalfFullOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            updateHalfFullOrderbook.OfficialBids.TryAdd(1m, 0.02m);
            updateHalfFullOrderbook.OfficialBids.TryAdd(2m, 0.15m);
            updateHalfFullOrderbook.OfficialBids.TryAdd(3m, 0.072382m);
            updateHalfFullOrderbook.OfficialBids.TryAdd(4m, 0.066716m);
            updateHalfFullOrderbook.OfficialBids.TryAdd(5m, 0.02m);

            updateHalfFullOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            updateHalfFullOrderbook.OfficialAsks.TryAdd(6m, 0.005m);
            updateHalfFullOrderbook.OfficialAsks.TryAdd(7m, 0.03m);
            updateHalfFullOrderbook.OfficialAsks.TryAdd(8m, 0.01759m);
            updateHalfFullOrderbook.OfficialAsks.TryAdd(9m, 0.03m);
            updateHalfFullOrderbook.OfficialAsks.TryAdd(10m, 0.03m);

            updateFullOrderbook.Sequence = 2;
            updateHalfFullOrderbook.Sequence = 2;

            updateFullOrderbook.Exchange = dummyExchange;
            updateHalfFullOrderbook.Exchange = dummyExchange;

            updateFullOrderbook.Symbol = "BTCUSDT";
            updateHalfFullOrderbook.Symbol = "BTCUSDT";

            //Act: run the following merge combinations:
            // empty beginning orderbook -> full update | full beginning -> half-full update | half-full beginning to full update

            beginningEmptyOrderbook.Merge(updateFullOrderbook);
            beginningFullOrderbook.Merge(updateHalfFullOrderbook);
            //beginningHalfFullOrderbook.Merge(updateFullOrderbook);

            //Assert: check that the beginning orderbooks now match the updates
            Assert.IsTrue(beginningEmptyOrderbook.OfficialAsks.Count == updateFullOrderbook.OfficialAsks.Count &&
                beginningEmptyOrderbook.OfficialBids.Count == updateFullOrderbook.OfficialBids.Count);

            Assert.IsTrue(beginningFullOrderbook.OfficialAsks.Count == updateHalfFullOrderbook.OfficialAsks.Count &&
                beginningFullOrderbook.OfficialBids.Count == updateHalfFullOrderbook.OfficialBids.Count);

            //Assert.IsTrue(beginningHalfFullOrderbook.OfficialAsks.Count == updateFullOrderbook.OfficialAsks.Count &&
            //    beginningHalfFullOrderbook.OfficialBids.Count == updateFullOrderbook.OfficialBids.Count);

            Assert.IsTrue(CheckLayers(updateFullOrderbook, beginningEmptyOrderbook));
            Assert.IsTrue(CheckLayers(updateHalfFullOrderbook, beginningFullOrderbook));
            //Assert.IsTrue(CheckLayers(updateFullOrderbook, beginningHalfFullOrderbook));

        }
        public bool CheckLayers(IOrderbook expected, IOrderbook actual)
        {
            bool layersMatch = true;
            foreach (var layer in expected.OfficialBids)
            {
                if (actual.OfficialBids.TryGetValue(layer.Key, out var testValue))
                {
                    if (layer.Value != testValue)
                    {
                        layersMatch = false;
                        Assert.Fail($"Expected value {layer.Value} was actually {testValue}");
                        return layersMatch;
                    }
                }
                else
                {
                    layersMatch = false;
                    Assert.Fail($"Expected key {layer.Key} not present");
                    return layersMatch;
                }
            }
            foreach (var layer in expected.OfficialAsks)
            {
                if (actual.OfficialAsks.TryGetValue(layer.Key, out var testValue))
                {
                    if (layer.Value != testValue)
                    {
                        layersMatch = false;
                        Assert.Fail($"Expected value {layer.Value} was actually {testValue}");
                        return layersMatch;
                    }
                }
                else
                {
                    layersMatch = false;
                    Assert.Fail($"Expected key {layer.Key} not present");
                    return layersMatch;
                }
            }
            return layersMatch;
        }
    }
}
