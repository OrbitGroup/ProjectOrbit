using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Serialization;
using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TriangleCollector.Services;
using System.Collections.Concurrent;
using TriangleCollector.Models;
using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Frameworks;
using Microsoft.AspNetCore.Builder;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TriangleCollector.UnitTests
{
    [TestClass]
    public class SymbolMappingTests
    {
        private static ILoggerFactory _factory = new NullLoggerFactory();
        private JsonElement.ArrayEnumerator apiResponse = RestAPIResponse.TestSymbolResponse();
        private OrderbookSubscriber testSubscriber = new OrderbookSubscriber(_factory, _factory.CreateLogger<OrderbookSubscriber>());
        private ConcurrentDictionary<string, List<Triangle>> actualSymbolTriangleMapping = TriangleCollector.SymbolTriangleMapping;

        [TestMethod]
        public void TestTriangleEligblePairs() //test that the symbolGenerator method properly identifies which symbols are eligible for triangular arbitrage
        {
            //Arrange: Expected outcomes are declared. 

            HashSet<String> expectedTriangleEligiblePairs = new HashSet<string>
            {"ETHBTC", "EOSETH", "EOSBTC", "EOSUSD", "BTCUSD"};

            //Act: run the sample API response through the SymbolGenerator

            testSubscriber.symbolGenerator(apiResponse);

            //Assert: confirm that the results of the test symbols match the expected outcome

            Assert.IsTrue(expectedTriangleEligiblePairs.SetEquals(TriangleCollector.triangleEligiblePairs));
        }
        [TestMethod]
        public void TestSymbolTriangleMapping() //test that all of the triangle eligible symbols are matched properly to all of their respective triangles
        {
            //Arrange: list all of the possible triangles and map them to their symbols
            List<Triangle> triangles = new List<Triangle>()
            {
                new Triangle("ETHBTC", "EOSETH", "EOSBTC", Triangle.Directions.BuyBuySell, _factory.CreateLogger<Triangle>()),
                new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>()),
                new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>())

            };

            var expectedSymbolTriangleMapping = new ConcurrentDictionary<string, List<Triangle>>();

            expectedSymbolTriangleMapping.GetOrAdd("ETHBTC", new List<Triangle>() { triangles[0], triangles[1] });
            expectedSymbolTriangleMapping.GetOrAdd("EOSETH", new List<Triangle>() { triangles[0], triangles[1] });
            expectedSymbolTriangleMapping.GetOrAdd("EOSBTC", new List<Triangle>() { triangles[0], triangles[1], triangles[2] });
            expectedSymbolTriangleMapping.GetOrAdd("BTCUSD", new List<Triangle>() { triangles[2] });
            expectedSymbolTriangleMapping.GetOrAdd("EOSUSD", new List<Triangle>() { triangles[2] });

            //Act: Run the sample API response through the SymbolGenerator
            testSubscriber.symbolGenerator(apiResponse);

            //Assert: confirm that the result of the test matches the expected outcome

            //First, test that the correct number of symbols are being mapped.
            Assert.IsTrue(expectedSymbolTriangleMapping.Count == actualSymbolTriangleMapping.Count, $"the wrong number of symbols were mapped. Expected: {expectedSymbolTriangleMapping.Count}. Actual: {actualSymbolTriangleMapping.Count}");

            //Next, for each symbol mapped, test that the correct number of triangles is matched to the symbol.
            foreach (var testItem in expectedSymbolTriangleMapping)
            {
                if (actualSymbolTriangleMapping.TryGetValue(testItem.Key, out List<Triangle> value))
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
                        if (value[0].FirstSymbol == "ETHBTC")
                        {
                            Assert.IsTrue(value[0].FirstSymbol == triangles[0].FirstSymbol, $"first symbol is incorrect. should be {triangles[0].FirstSymbol}, is {value[0].FirstSymbol}");
                            Assert.IsTrue(value[0].SecondSymbol == triangles[0].SecondSymbol, $"second symbol is incorrect. should be {triangles[0].SecondSymbol}, is {value[0].SecondSymbol}");
                            Assert.IsTrue(value[0].ThirdSymbol == triangles[0].ThirdSymbol, $"third symbol is incorrect. should be {triangles[0].ThirdSymbol}, is {value[0].ThirdSymbol}");
                            Assert.IsTrue(value[0].Direction == triangles[0].Direction, $"direction is incorrect. should be {triangles[0].Direction}, is {value[0].Direction}");

                            Assert.IsTrue(value[1].FirstSymbol == triangles[1].FirstSymbol, $"first symbol is incorrect. should be {triangles[1].FirstSymbol}, is {value[1].FirstSymbol}");
                            Assert.IsTrue(value[1].SecondSymbol == triangles[1].SecondSymbol, $"second symbol is incorrect. should be {triangles[1].SecondSymbol}, is {value[1].SecondSymbol}");
                            Assert.IsTrue(value[1].ThirdSymbol == triangles[1].ThirdSymbol, $"third symbol is incorrect. should be {triangles[1].ThirdSymbol}, is {value[1].ThirdSymbol}");
                            Assert.IsTrue(value[1].Direction == triangles[1].Direction, $"direction is incorrect. should be {triangles[1].Direction}, is {value[1].Direction}");
                        } else if (value[0].FirstSymbol == "EOSBTC")
                        {
                            Assert.IsTrue(value[0].FirstSymbol == triangles[1].FirstSymbol, $"first symbol is incorrect. should be {triangles[1].FirstSymbol}, is {value[0].FirstSymbol}");
                            Assert.IsTrue(value[0].SecondSymbol == triangles[1].SecondSymbol, $"second symbol is incorrect. should be {triangles[1].SecondSymbol}, is {value[0].SecondSymbol}");
                            Assert.IsTrue(value[0].ThirdSymbol == triangles[1].ThirdSymbol, $"third symbol is incorrect. should be {triangles[1].ThirdSymbol}, is {value[0].ThirdSymbol}");
                            Assert.IsTrue(value[0].Direction == triangles[1].Direction, $"direction is incorrect. should be {triangles[1].Direction}, is {value[0].Direction}");

                            Assert.IsTrue(value[1].FirstSymbol == triangles[0].FirstSymbol, $"first symbol is incorrect. should be {triangles[0].FirstSymbol}, is {value[1].FirstSymbol}");
                            Assert.IsTrue(value[1].SecondSymbol == triangles[0].SecondSymbol, $"second symbol is incorrect. should be {triangles[0].SecondSymbol}, is {value[1].SecondSymbol}");
                            Assert.IsTrue(value[1].ThirdSymbol == triangles[0].ThirdSymbol, $"third symbol is incorrect. should be {triangles[0].ThirdSymbol}, is {value[1].ThirdSymbol}");
                            Assert.IsTrue(value[1].Direction == triangles[0].Direction, $"direction is incorrect. should be {triangles[0].Direction}, is {value[1].Direction}");
                        }
                    } 
                }
            }
        }
    }
}