using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TriangleCollector.Models.Exchanges.Huobi;
using TriangleCollector.Models.Interfaces;
using System.Collections.Concurrent;

namespace TriangleCollector.UnitTests
{
    [TestClass]
    public class HuobiMergeTests
    {
        [TestMethod]
        public void TestHuobiOrderbookDeserializer()
        {
            //Arrange: 
            //sample websocket message from Huobi for an orderbook refresh:
            string sampleWebsocketMessage = "{\"ch\":\"market.btcusdt.mbp.refresh.10\",\"ts\":1611504245560,\"tick\":{\"seqNum\":119437040567,\"bids\":[[32115.28,0.02],[32115.13,0.15],[32114.05,0.072382],[32113.94,0.066716],[32113.61,0.02],[32113.52,2.521393],[32113.48,0.117404],[32109.42,0.139009],[32108.76,0.123],[32108.25,0.11175]],\"asks\":[[32115.29,0.005],[32115.69,0.03],[32116.2,0.01759],[32118.17,0.03],[32118.86,0.03],[32119.61,0.12],[32120.41,0.08],[32121.51,0.125775],[32121.64,0.03563],[32124.4,0.001]]}}";
            //expected deserialized orderbook:
            var expectedOrderbook = new HuobiOrderbook();
            expectedOrderbook.Sequence = 119437040567;
            expectedOrderbook.Symbol = "BTCUSDT";

            expectedOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            expectedOrderbook.OfficialBids.TryAdd(32115.28m, 0.02m);
            expectedOrderbook.OfficialBids.TryAdd(32115.13m, 0.15m);
            expectedOrderbook.OfficialBids.TryAdd(32114.05m, 0.072382m);
            expectedOrderbook.OfficialBids.TryAdd(32113.94m, 0.066716m);
            expectedOrderbook.OfficialBids.TryAdd(32113.61m, 0.02m);
            expectedOrderbook.OfficialBids.TryAdd(32113.52m, 2.521393m);
            expectedOrderbook.OfficialBids.TryAdd(32113.48m, 0.117404m);
            expectedOrderbook.OfficialBids.TryAdd(32109.42m, 0.139009m);
            expectedOrderbook.OfficialBids.TryAdd(32108.76m, 0.123m);
            expectedOrderbook.OfficialBids.TryAdd(32108.25m, 0.11175m);

            expectedOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            expectedOrderbook.OfficialAsks.TryAdd(32115.29m, 0.005m);
            expectedOrderbook.OfficialAsks.TryAdd(32115.69m, 0.03m);
            expectedOrderbook.OfficialAsks.TryAdd(32116.2m, 0.01759m);
            expectedOrderbook.OfficialAsks.TryAdd(32118.17m, 0.03m);
            expectedOrderbook.OfficialAsks.TryAdd(32118.86m, 0.03m);
            expectedOrderbook.OfficialAsks.TryAdd(32119.61m, 0.12m);
            expectedOrderbook.OfficialAsks.TryAdd(32120.41m, 0.08m);
            expectedOrderbook.OfficialAsks.TryAdd(32121.51m, 0.125775m);
            expectedOrderbook.OfficialAsks.TryAdd(32121.64m, 0.03563m);
            expectedOrderbook.OfficialAsks.TryAdd(32124.4m, 0.001m);

            //Act: run the sample websocket message through the Huobi OrderbookConverter
            IOrderbook testOrderbook = (IOrderbook)JsonSerializer.Deserialize(sampleWebsocketMessage, typeof(HuobiOrderbook));

            //Assert: test that the actual outcome matches the expected outcome
            Assert.AreEqual(expectedOrderbook.Symbol, testOrderbook.Symbol);
            Assert.AreEqual(expectedOrderbook.Sequence, testOrderbook.Sequence);
            Assert.IsTrue(expectedOrderbook.OfficialAsks.Count == testOrderbook.OfficialAsks.Count &&
                expectedOrderbook.OfficialBids.Count == testOrderbook.OfficialBids.Count);
            
            Assert.IsTrue(CheckLayers(expectedOrderbook, testOrderbook));
        }
        [TestMethod]
        public void TestHuobiOrderbookMerge()
        {
            //Arrange
            //Beginning Orderbooks: one empty, one with 10 values, one with 5 values
            var dummyExchange = new HuobiExchange("huobi");
            dummyExchange.ProfitableSymbolMapping.TryAdd("dummy", 0);

            var beginningEmptyOrderbook = new HuobiOrderbook();
            

            var beginningFullOrderbook = new HuobiOrderbook();
            beginningFullOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            beginningFullOrderbook.OfficialBids.TryAdd(32115.28m, 0.02m);
            beginningFullOrderbook.OfficialBids.TryAdd(32115.13m, 0.15m);
            beginningFullOrderbook.OfficialBids.TryAdd(32114.05m, 0.072382m);
            beginningFullOrderbook.OfficialBids.TryAdd(32113.94m, 0.066716m);
            beginningFullOrderbook.OfficialBids.TryAdd(32113.61m, 0.02m);
            beginningFullOrderbook.OfficialBids.TryAdd(32113.52m, 2.521393m);
            beginningFullOrderbook.OfficialBids.TryAdd(32113.48m, 0.117404m);
            beginningFullOrderbook.OfficialBids.TryAdd(32109.42m, 0.139009m);
            beginningFullOrderbook.OfficialBids.TryAdd(32108.76m, 0.123m);
            beginningFullOrderbook.OfficialBids.TryAdd(32108.25m, 0.11175m);

            beginningFullOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            beginningFullOrderbook.OfficialAsks.TryAdd(32115.29m, 0.005m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32115.69m, 0.03m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32116.2m, 0.01759m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32118.17m, 0.03m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32118.86m, 0.03m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32119.61m, 0.12m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32120.41m, 0.08m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32121.51m, 0.125775m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32121.64m, 0.03563m);
            beginningFullOrderbook.OfficialAsks.TryAdd(32124.4m, 0.001m);

            var beginningHalfFullOrderbook = new HuobiOrderbook();
            beginningHalfFullOrderbook.OfficialBids = new ConcurrentDictionary<decimal, decimal>();
            beginningHalfFullOrderbook.OfficialBids.TryAdd(32115.28m, 0.02m);
            beginningHalfFullOrderbook.OfficialBids.TryAdd(32115.13m, 0.15m);
            beginningHalfFullOrderbook.OfficialBids.TryAdd(32114.05m, 0.072382m);
            beginningHalfFullOrderbook.OfficialBids.TryAdd(32113.94m, 0.066716m);
            beginningHalfFullOrderbook.OfficialBids.TryAdd(32113.61m, 0.02m);
            
            beginningHalfFullOrderbook.OfficialAsks = new ConcurrentDictionary<decimal, decimal>();
            beginningHalfFullOrderbook.OfficialAsks.TryAdd(32115.29m, 0.005m);
            beginningHalfFullOrderbook.OfficialAsks.TryAdd(32115.69m, 0.03m);
            beginningHalfFullOrderbook.OfficialAsks.TryAdd(32116.2m, 0.01759m);
            beginningHalfFullOrderbook.OfficialAsks.TryAdd(32118.17m, 0.03m);
            beginningHalfFullOrderbook.OfficialAsks.TryAdd(32118.86m, 0.03m);

            beginningEmptyOrderbook.Symbol = "BTCUSDT";
            beginningHalfFullOrderbook.Symbol = "BTCUSDT";
            beginningFullOrderbook.Symbol = "BTCUSDT";

            beginningEmptyOrderbook.Sequence = 1;
            beginningHalfFullOrderbook.Sequence = 1;
            beginningFullOrderbook.Sequence = 1;

            beginningEmptyOrderbook.Exchange = dummyExchange;
            beginningHalfFullOrderbook.Exchange = dummyExchange;
            beginningFullOrderbook.Exchange = dummyExchange;

            //arrange orderbook updates: one empty, one full, one with 6 values
            var updateFullOrderbook = new HuobiOrderbook();
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

            var updateHalfFullOrderbook = new HuobiOrderbook();
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
            beginningHalfFullOrderbook.Merge(updateFullOrderbook);

            //Assert: check that the beginning orderbooks now match the updates
            Assert.IsTrue(beginningEmptyOrderbook.OfficialAsks.Count == updateFullOrderbook.OfficialAsks.Count &&
                beginningEmptyOrderbook.OfficialBids.Count == updateFullOrderbook.OfficialBids.Count);

            Assert.IsTrue(beginningFullOrderbook.OfficialAsks.Count == updateHalfFullOrderbook.OfficialAsks.Count &&
                beginningFullOrderbook.OfficialBids.Count == updateHalfFullOrderbook.OfficialBids.Count);

            Assert.IsTrue(beginningHalfFullOrderbook.OfficialAsks.Count == updateFullOrderbook.OfficialAsks.Count &&
                beginningHalfFullOrderbook.OfficialBids.Count == updateFullOrderbook.OfficialBids.Count);

            Assert.IsTrue(CheckLayers(updateFullOrderbook, beginningEmptyOrderbook));
            Assert.IsTrue(CheckLayers(updateHalfFullOrderbook, beginningFullOrderbook));
            Assert.IsTrue(CheckLayers(updateFullOrderbook, beginningHalfFullOrderbook));

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
