using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using System.Collections.Generic;
using TriangleCollector.Services;
using System.Collections.Concurrent;
using TriangleCollector.Models;
using TriangleCollector.Models.Exchange_Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TriangleCollector.UnitTests.Models;

namespace TriangleCollector.UnitTests
{
    [TestClass]
    public class SymbolMappingTests
    {
        private static ILoggerFactory _factory = new NullLoggerFactory();

        [TestMethod]
        public void NEWTestTriangleEligiblePairs()
        {
            //Arrange: Expected outcomes are declared. 

            HashSet<Orderbook> expectedTriangleEligiblePairs = new HashSet<Orderbook>(); //"ETHBTC", "EOSETH", "EOSBTC", "EOSUSD", "BTCUSD"
            var ethbtc = new Orderbook();
            ethbtc.symbol = "ETHBTC";
            ethbtc.quoteCurrency = "BTC";
            ethbtc.baseCurrency= "ETH";
            expectedTriangleEligiblePairs.Add(ethbtc);

            var eoseth = new Orderbook();
            eoseth.symbol = "EOSETH";
            eoseth.quoteCurrency = "ETH";
            eoseth.baseCurrency = "EOS";
            expectedTriangleEligiblePairs.Add(eoseth);

            var eosbtc = new Orderbook();
            eosbtc.symbol = "EOSBTC";
            eosbtc.quoteCurrency = "BTC";
            eosbtc.baseCurrency = "EOS";
            expectedTriangleEligiblePairs.Add(eosbtc);

            var eosusd = new Orderbook();
            eosusd.symbol = "EOSUSD";
            eosusd.quoteCurrency = "USD";
            eosusd.baseCurrency = "EOS";
            expectedTriangleEligiblePairs.Add(eosusd);

            var btcusd = new Orderbook();
            btcusd.symbol = "BTCUSD";
            btcusd.quoteCurrency = "USD";
            btcusd.baseCurrency = "BTC";
            expectedTriangleEligiblePairs.Add(btcusd);

            //Act: run the sample API response through the function

            var testExchange = new Exchange("hitbtc");
            testExchange.triarbEligibleMarkets.Clear();
            testExchange.tradedMarkets = expectedTriangleEligiblePairs;
            testExchange.mapOpportunities();

            //Assert: confirm that the results of the test symbols match the expected outcome

            Assert.IsTrue(expectedTriangleEligiblePairs.SetEquals(testExchange.triarbEligibleMarkets));
        }
        
        [TestMethod]
        public void NEWTestSymbolTriangleMapping() //test that all of the triangle eligible symbols are matched properly to all of their respective triangles
        {

            //Arrange: list all of the possible triangles and map them to their symbols
            List<Triangle> triangles = new List<Triangle>()
            {
                new Triangle("ETHBTC", "EOSETH", "EOSBTC", Triangle.Directions.BuyBuySell, _factory.CreateLogger<Triangle>()),
                new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>()),
                new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>())

            };

            HashSet<Orderbook> expectedTriangleEligiblePairs = new HashSet<Orderbook>(); //"ETHBTC", "EOSETH", "EOSBTC", "EOSUSD", "BTCUSD"
            var ethbtc = new Orderbook();
            ethbtc.symbol = "ETHBTC";
            ethbtc.quoteCurrency = "BTC";
            ethbtc.baseCurrency = "ETH";
            expectedTriangleEligiblePairs.Add(ethbtc);

            var eoseth = new Orderbook();
            eoseth.symbol = "EOSETH";
            eoseth.quoteCurrency = "ETH";
            eoseth.baseCurrency = "EOS";
            expectedTriangleEligiblePairs.Add(eoseth);

            var eosbtc = new Orderbook();
            eosbtc.symbol = "EOSBTC";
            eosbtc.quoteCurrency = "BTC";
            eosbtc.baseCurrency = "EOS";
            expectedTriangleEligiblePairs.Add(eosbtc);

            var eosusd = new Orderbook();
            eosusd.symbol = "EOSUSD";
            eosusd.quoteCurrency = "USD";
            eosusd.baseCurrency = "EOS";
            expectedTriangleEligiblePairs.Add(eosusd);

            var btcusd = new Orderbook();
            btcusd.symbol = "BTCUSD";
            btcusd.quoteCurrency = "USD";
            btcusd.baseCurrency = "BTC";
            expectedTriangleEligiblePairs.Add(btcusd);


            var expectedSymbolTriangleMapping = new ConcurrentDictionary<string, List<Triangle>>();

            expectedSymbolTriangleMapping.GetOrAdd("ETHBTC", new List<Triangle>() { triangles[0], triangles[1] });
            expectedSymbolTriangleMapping.GetOrAdd("EOSETH", new List<Triangle>() { triangles[0], triangles[1] });
            expectedSymbolTriangleMapping.GetOrAdd("EOSBTC", new List<Triangle>() { triangles[0], triangles[1], triangles[2] });
            expectedSymbolTriangleMapping.GetOrAdd("BTCUSD", new List<Triangle>() { triangles[2] });
            expectedSymbolTriangleMapping.GetOrAdd("EOSUSD", new List<Triangle>() { triangles[2] });

            //Act: Run the sample API response through the function
            var testExchange = new Exchange("hitbtc");
            testExchange.triarbEligibleMarkets.Clear();
            testExchange.triarbMarketMapping.Clear();
            testExchange.tradedMarkets = expectedTriangleEligiblePairs;
            testExchange.mapOpportunities();

            //Assert: confirm that the result of the test matches the expected outcome

            //First, test that the correct number of symbols are being mapped.
            Assert.IsTrue(expectedSymbolTriangleMapping.Count == testExchange.triarbMarketMapping.Count, $"the wrong number of symbols were mapped. Expected: {expectedSymbolTriangleMapping.Count}. Actual: {testExchange.triarbMarketMapping.Count}");

            //Next, for each symbol mapped, test that the correct number of triangles is matched to the symbol.
            foreach (var testItem in expectedSymbolTriangleMapping)
            {
                if (testExchange.triarbMarketMapping.TryGetValue(testItem.Key, out List<Triangle> value))
                {
                    Assert.IsTrue(testItem.Value.Count == value.Count, $"the wrong number of triangles were mapped to a symbol. For {testItem.Key}, expected was {testItem.Value.Count}, but the actual count was {value.Count}");

                    //Now, for each symbol mapped, test that the correct triangles are being mapped to the correct symbols. This is really manual but I couldn't think of a better way
                    if (testItem.Key == "EOSUSD" || testItem.Key == "BTCUSD")
                    {
                        Assert.IsTrue(value[0].FirstSymbol == triangles[2].FirstSymbol, $"first symbol is incorrect. should be {triangles[2].FirstSymbol}, is {value[0].FirstSymbol}");
                        Assert.IsTrue(value[0].SecondSymbol == triangles[2].SecondSymbol, $"second symbol is incorrect. should be {triangles[2].SecondSymbol}, is {value[0].SecondSymbol}");
                        Assert.IsTrue(value[0].ThirdSymbol == triangles[2].ThirdSymbol, $"third symbol is incorrect. should be {triangles[2].ThirdSymbol}, is {value[0].ThirdSymbol}");
                        Assert.IsTrue(value[0].Direction == triangles[2].Direction, $"direction is incorrect. should be {triangles[2].Direction}, is {value[0].Direction}");

                    }
                    else if (testItem.Key == "ETHBTC" || testItem.Key == "EOSETH")
                    {
                        Assert.IsTrue(value.Count == 2);
                        Assert.IsTrue(value[1].FirstSymbol == triangles[1].FirstSymbol, $"first symbol is incorrect. should be {triangles[1].FirstSymbol}, is {value[1].FirstSymbol}");
                        Assert.IsTrue(value[1].SecondSymbol == triangles[1].SecondSymbol, $"second symbol is incorrect. should be {triangles[1].SecondSymbol}, is {value[1].SecondSymbol}");
                        Assert.IsTrue(value[1].ThirdSymbol == triangles[1].ThirdSymbol, $"third symbol is incorrect. should be {triangles[1].ThirdSymbol}, is {value[1].ThirdSymbol}");
                        Assert.IsTrue(value[1].Direction == triangles[1].Direction, $"direction is incorrect. should be {triangles[1].Direction}, is {value[1].Direction}");

                        Assert.IsTrue(value[0].FirstSymbol == triangles[0].FirstSymbol, $"first symbol is incorrect. should be {triangles[0].FirstSymbol}, is {value[0].FirstSymbol}");
                        Assert.IsTrue(value[0].SecondSymbol == triangles[0].SecondSymbol, $"second symbol is incorrect. should be {triangles[0].SecondSymbol}, is {value[0].SecondSymbol}");
                        Assert.IsTrue(value[0].ThirdSymbol == triangles[0].ThirdSymbol, $"third symbol is incorrect. should be {triangles[0].ThirdSymbol}, is {value[0].ThirdSymbol}");
                        Assert.IsTrue(value[0].Direction == triangles[0].Direction, $"direction is incorrect. should be {triangles[0].Direction}, is {value[0].Direction}");
                    }
                    else if (testItem.Key == "EOSBTC")
                    {
                        Assert.IsTrue(value.Count == 3);
                        Assert.IsTrue(value[1].FirstSymbol == triangles[1].FirstSymbol, $"first symbol is incorrect. should be {triangles[1].FirstSymbol}, is {value[1].FirstSymbol}");
                        Assert.IsTrue(value[1].SecondSymbol == triangles[1].SecondSymbol, $"second symbol is incorrect. should be {triangles[1].SecondSymbol}, is {value[1].SecondSymbol}");
                        Assert.IsTrue(value[1].ThirdSymbol == triangles[1].ThirdSymbol, $"third symbol is incorrect. should be {triangles[1].ThirdSymbol}, is {value[1].ThirdSymbol}");
                        Assert.IsTrue(value[1].Direction == triangles[1].Direction, $"direction is incorrect. should be {triangles[1].Direction}, is {value[1].Direction}");

                        Assert.IsTrue(value[0].FirstSymbol == triangles[0].FirstSymbol, $"first symbol is incorrect. should be {triangles[0].FirstSymbol}, is {value[0].FirstSymbol}");
                        Assert.IsTrue(value[0].SecondSymbol == triangles[0].SecondSymbol, $"second symbol is incorrect. should be {triangles[0].SecondSymbol}, is {value[0].SecondSymbol}");
                        Assert.IsTrue(value[0].ThirdSymbol == triangles[0].ThirdSymbol, $"third symbol is incorrect. should be {triangles[0].ThirdSymbol}, is {value[0].ThirdSymbol}");
                        Assert.IsTrue(value[0].Direction == triangles[0].Direction, $"direction is incorrect. should be {triangles[0].Direction}, is {value[0].Direction}");

                        Assert.IsTrue(value[2].FirstSymbol == triangles[2].FirstSymbol, $"first symbol is incorrect. should be {triangles[2].FirstSymbol}, is {value[2].FirstSymbol}");
                        Assert.IsTrue(value[2].SecondSymbol == triangles[2].SecondSymbol, $"second symbol is incorrect. should be {triangles[2].SecondSymbol}, is {value[2].SecondSymbol}");
                        Assert.IsTrue(value[2].ThirdSymbol == triangles[2].ThirdSymbol, $"third symbol is incorrect. should be {triangles[2].ThirdSymbol}, is {value[2].ThirdSymbol}");
                        Assert.IsTrue(value[2].Direction == triangles[2].Direction, $"direction is incorrect. should be {triangles[2].Direction}, is {value[2].Direction}");
                    }
                    else
                    {
                        Assert.Fail($"Unexpected case {testItem.Key}");
                    }
                }
                else
                {
                    Assert.Fail($"Expected symbol {testItem.Key} but did not find it in actual mapping.");
                }
            }
        }
    }
}