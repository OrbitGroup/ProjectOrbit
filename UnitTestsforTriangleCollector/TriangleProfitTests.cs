using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using TriangleCollector.Models;
using TriangleCollector.UnitTests.Models;

namespace TriangleCollector.UnitTests
{
    [TestClass]
    public class TriangleProfitTests
    {
        private static ILoggerFactory _factory = new NullLoggerFactory();

        public Triangle EthEosBtc = new Triangle("ETHBTC", "EOSETH", "EOSBTC", Triangle.Directions.BuyBuySell, _factory.CreateLogger<Triangle>());
        public decimal EthEosBtcProfit = 0.9924677859176047787362868849m;


        public Triangle EosEthBtc = new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>());
        public decimal EosEthBtcProfit = 0.9994362518556066475976682718m;

        public Triangle UsdEosBtc = new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>());
        public decimal UsdEosBtcProfit = 0.9994800007008076808521821399m;

        [TestMethod]
        public void TestGetProfitPercentNoInputBuyBuySell()
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtc;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtc;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcProfit, EthEosBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestGetProfitPercentNoInputSellBuySell()
        {
            UsdEosBtc.FirstSymbolOrderbook = Orderbooks.BtcUsdSortedBids;
            UsdEosBtc.SecondSymbolOrderbook = Orderbooks.EosUsdSortedAsks;
            UsdEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtc;

            UsdEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(UsdEosBtcProfit, UsdEosBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestGetProfitPercentNoInputBuySellSell()
        {
            EosEthBtc.FirstSymbolOrderbook = Orderbooks.EosBtc;
            EosEthBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EosEthBtc.ThirdSymbolOrderbook = Orderbooks.EthBtc;

            EosEthBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EosEthBtcProfit, EosEthBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestGetProfitPercentWithInputBuyBuySell()
        {

        }

        [TestMethod]
        public void TestGetProfitPercentWithInputSellBuySell()
        {

        }

        [TestMethod]
        public void TestGetProfitPercentWithInputBuySellSell()
        {

        }
    }
}
