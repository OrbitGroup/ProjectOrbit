﻿using Microsoft.Extensions.Logging;
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
        public decimal EthEosBtcUnprofitableProfit = 0.9924677859176047787362868849m - 1;
        public decimal EthEosBtcUnprofitableVolume = 0.005536389908m;
        //Profitable expected outcomes
        public decimal EthEosBtcProfitableProfitPercent = 0.007280446851m; //profit expressed as a percentage of volume traded
        public decimal EthEosBtcProfitableProfit = 0.0008956347473m; //total profit returned in BTC
        public decimal EthEosBtcProfitableVolume = 0.1230192m; //total volume traded

        public Triangle EosEthBtc = new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>());
        public decimal EosEthBtcUnprofitableProfit = 0.9994362518556066475976682718m - 1;
        public decimal EosEthBtcUnprofitableVolume = 0.005520685968m;

        public Triangle UsdEosBtc = new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>());
        public decimal UsdEosBtcUnprofitableProfit = 0.9994800007008076808521821399m - 1;
        public decimal UsdEosBtcUnprofitableVolume = 0.01019975m;


        [TestMethod]
        public void TestGetProfitPercentNoInputBuyBuySell() // without any input, this tests the ProfitPercent output of an unprofitable triangle.
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtc;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcUnprofitable;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcUnprofitableProfit, EthEosBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestVolumeNoInputBuyBuySell() // without any input, this tests the MaxVolume output of an unprofitable triangle.
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtc;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcUnprofitable;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcUnprofitableVolume, EthEosBtc.MaxVolume);
        }

        [TestMethod]
        public void TestGetProfitPercentNoInputSellBuySell()
        {
            UsdEosBtc.FirstSymbolOrderbook = Orderbooks.BtcUsdSortedBids;
            UsdEosBtc.SecondSymbolOrderbook = Orderbooks.EosUsdSortedAsks;
            UsdEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcUnprofitable;

            UsdEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(UsdEosBtcUnprofitableProfit, UsdEosBtc.ProfitPercent);
        }
        
        [TestMethod]
        public void TestMaxVolumeNoInputSellBuySell()
        {
            UsdEosBtc.FirstSymbolOrderbook = Orderbooks.BtcUsdSortedBids;
            UsdEosBtc.SecondSymbolOrderbook = Orderbooks.EosUsdSortedAsks;
            UsdEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcUnprofitable;

            UsdEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(UsdEosBtcUnprofitableVolume, UsdEosBtc.MaxVolume);
        }

        [TestMethod]
        public void TestGetProfitPercentNoInputBuySellSell()
        {
            EosEthBtc.FirstSymbolOrderbook = Orderbooks.EosBtcUnprofitable;
            EosEthBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EosEthBtc.ThirdSymbolOrderbook = Orderbooks.EthBtc;

            EosEthBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EosEthBtcUnprofitableProfit, EosEthBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestMaxVolumeNoInputBuySellSell()
        {
            EosEthBtc.FirstSymbolOrderbook = Orderbooks.EosBtcUnprofitable;
            EosEthBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EosEthBtc.ThirdSymbolOrderbook = Orderbooks.EthBtc;

            EosEthBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EosEthBtcUnprofitableVolume, EosEthBtc.MaxVolume);
        }

        [TestMethod]
        public void TestLayersBuyBuySell() //only profitable triangles require inputs - volume is calculated as well
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtc;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcProfitable;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcProfitableProfit, EthEosBtc.Profit);
            Assert.AreEqual(EthEosBtcProfitableVolume, EthEosBtc.MaxVolume);
            Assert.AreEqual(EthEosBtcProfitableProfitPercent, EthEosBtc.ProfitPercent);
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
