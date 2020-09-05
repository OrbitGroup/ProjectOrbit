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


namespace TriangleCollector.UnitTests
{
    [TestClass]
    public class SymbolMappingTest
    {
        private ILoggerFactory _factory; 
        //ISSUE: _factory is never assigned to and therefore is null, but I can't figure out how to assign the right value to it 
        //the TriangleCollector seems to create an instance of iloggerfactory using CreateHostBuilder and then passes that to OrderbookSubscriber - how do we do that here?

        [TestMethod]
        public void TestSymbolMapping()
        {
            //Arrange: 'AssembleResponse' creates a sample API response to test. Expected outcomes are declared.
            //      Symbol Generator populates the hashset TriangleEligiblePairs and the concurrent dictionary SymbolTriangleMapping.
            var apiResponse = RestAPIResponse.TestSymbolResponse();
            HashSet<String> expectedTriangleEligiblePairs = new HashSet<string>
            {"ETHBTC", "EOSETH", "EOSBTC", "EOSUSD", "BTCUSD"};

            //Act: run the sample API response through the SymbolGenerator

            OrderbookSubscriber testSubscriber = new OrderbookSubscriber(_factory, _factory.CreateLogger<OrderbookSubscriber>());

            testSubscriber.symbolGenerator(apiResponse);

            //Assert: confirm that the results of the test symbols match the expected outcome
            Assert.AreEqual(expectedTriangleEligiblePairs, TriangleCollector.triangleEligiblePairs);
        }
    }
}



    /* 
     * 
     * BuyBuySell:
     * {"id":"ETHBTC","baseCurrency":"ETH","quoteCurrency":"BTC","quantityIncrement":"0.0001","tickSize":"0.000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"BTC"}
     * {"id":"EOSETH","baseCurrency":"EOS","quoteCurrency":"ETH","quantityIncrement":"0.01","tickSize":"0.0000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"ETH"}
     * {"id":"EOSBTC","baseCurrency":"EOS","quoteCurrency":"BTC","quantityIncrement":"0.01","tickSize":"0.00000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"BTC"}
     * 
     * 
     * SellBuySell
     * {"id":"BTCUSD","baseCurrency":"BTC","quoteCurrency":"USD","quantityIncrement":"0.00001","tickSize":"0.01","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"USD"}
     * {"id":"CURUSD","baseCurrency":"CUR","quoteCurrency":"USD","quantityIncrement":"0.1","tickSize":"0.000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"USD"}
     * {"id":"CURBTC","baseCurrency":"CUR","quoteCurrency":"BTC","quantityIncrement":"0.1","tickSize":"0.000000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"BTC"}
     * 
     * {"id":"NANOBTC","baseCurrency":"NANO","quoteCurrency":"BTC","quantityIncrement":"0.01","tickSize":"0.00000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"BTC"},
     * {"id":"NANOETH","baseCurrency":"NANO","quoteCurrency":"ETH","quantityIncrement":"0.01","tickSize":"0.0000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"ETH"},
     * {"id":"ETHBTC","baseCurrency":"ETH","quoteCurrency":"BTC","quantityIncrement":"0.0001","tickSize":"0.000001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"BTC"}
     * 
     * {"id":"EOSUSD","baseCurrency":"EOS","quoteCurrency":"USD","quantityIncrement":"0.01","tickSize":"0.00001","takeLiquidityRate":"0.0025","provideLiquidityRate":"0.001","feeCurrency":"USD"}
     * 
     * */
