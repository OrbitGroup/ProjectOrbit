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

        //unprofitable triangles
        public Triangle EthEosBtc = new Triangle("ETHBTC", "EOSETH", "EOSBTC", Triangle.Directions.BuyBuySell, _factory.CreateLogger<Triangle>());
        public decimal EthEosBtcUnprofitableProfit = 0.9924677859176047787362868849m - 1;
        public decimal EthEosBtcUnprofitableVolume = 0.005536389908m;

        public Triangle EosEthBtc = new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>());
        public decimal EosEthBtcUnprofitableProfit = 0.9994362518556066475976682718m - 1;
        public decimal EosEthBtcUnprofitableVolume = 0.005520685968m;

        public Triangle UsdEosBtc = new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>());
        public decimal UsdEosBtcUnprofitableProfit = 0.9994800007008076808521821399m - 1;
        public decimal UsdEosBtcUnprofitableVolume = 0.01019975m;

        //Profitable expected outcomes: For each direction and bottleneck number 
        //BuyBuySell - Bottleneck is 1
        public decimal EthEosBtcProfitableBottleneckOneProfitPercent = 0.0115712778632038277275946787m; //profit expressed as a percentage of volume traded
        public decimal EthEosBtcProfitableBottleneckOneProfit = 0.0001422560403522186320696544m; //total profit returned in BTC
        public decimal EthEosBtcProfitableBottleneckOneVolume = 0.012293892m; //total volume traded

        //BuyBuySell - Bottleneck is 2
        public decimal EthEosBtcProfitableBottleneckTwoProfitPercent = 0.0072804468509875898859648437m; //profit expressed as a percentage of volume traded
        public decimal EthEosBtcProfitableBottleneckTwoProfit = 0.0008956347472510125176994863m; //total profit returned in BTC
        public decimal EthEosBtcProfitableBottleneckTwoVolume = 0.1230192m; //total volume traded

        //BuyBuySell - Bottleneck is 3
        public decimal EthEosBtcProfitableBottleneckThreeProfitPercent = 0.0092601928062477359015903732m; //profit expressed as a percentage of volume traded
        public decimal EthEosBtcProfitableBottleneckThreeProfit = 0.0035020514316271301617589103m; //total profit returned in BTC
        public decimal EthEosBtcProfitableBottleneckThreeVolume = 0.378183425m; //total volume traded

        //BuySellSell - Bottleneck is 2
        public decimal EosEthBtcProfitableBottleneckTwoProfitPercent = 0.01977536775m; //profit expressed as a percentage of volume traded
        public decimal EosEthBtcProfitableBottleneckTwoProfit = 0.001223931524m; //total profit returned in BTC
        public decimal EosEthBtcProfitableBottleneckTwoVolume = 0.06189172m; //total volume traded

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
        public void TestLayersBuyBuySellBottleneckTwo() //only profitable triangles require inputs - volume is calculated as well
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtc;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcProfitable;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcProfitableBottleneckTwoProfit, EthEosBtc.Profit);
            Assert.AreEqual(EthEosBtcProfitableBottleneckTwoVolume, EthEosBtc.MaxVolume);
            Assert.AreEqual(EthEosBtcProfitableBottleneckTwoProfitPercent, EthEosBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestLayersBuyBuySellBottleneckOne() //only profitable triangles require inputs - volume is calculated as well
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtcBuyBuySellBottleneckOne;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEthBuyBuySellBottleneckOne;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcProfitable;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcProfitableBottleneckOneProfit, EthEosBtc.Profit);
            Assert.AreEqual(EthEosBtcProfitableBottleneckOneVolume, EthEosBtc.MaxVolume);
            Assert.AreEqual(EthEosBtcProfitableBottleneckOneProfitPercent, EthEosBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestLayersBuyBuySellBottleneckThree() //only profitable triangles require inputs - volume is calculated as well
        {
            EthEosBtc.FirstSymbolOrderbook = Orderbooks.EthBtcBuyBuySellBottleneckThree;
            EthEosBtc.SecondSymbolOrderbook = Orderbooks.EosEthBuyBuySellBottleneckOne;
            EthEosBtc.ThirdSymbolOrderbook = Orderbooks.EosBtcProfitable;

            EthEosBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EthEosBtcProfitableBottleneckThreeProfit, EthEosBtc.Profit);
            Assert.AreEqual(EthEosBtcProfitableBottleneckThreeVolume, EthEosBtc.MaxVolume);
            Assert.AreEqual(EthEosBtcProfitableBottleneckThreeProfitPercent, EthEosBtc.ProfitPercent);
        }

        [TestMethod]
        public void TestLayersBuySellSellBottleneckTwo() //only profitable triangles require inputs - volume is calculated as well
        {
            EosEthBtc.FirstSymbolOrderbook = Orderbooks.EosBtcProfitable;
            EosEthBtc.SecondSymbolOrderbook = Orderbooks.EosEth;
            EosEthBtc.ThirdSymbolOrderbook = Orderbooks.EthBtc;

            EosEthBtc.SetMaxVolumeAndProfitability();
            Assert.AreEqual(EosEthBtcProfitableBottleneckTwoProfit, EosEthBtc.Profit);
            Assert.AreEqual(EosEthBtcProfitableBottleneckTwoVolume, EosEthBtc.MaxVolume);
            Assert.AreEqual(EosEthBtcProfitableBottleneckTwoProfitPercent, EosEthBtc.ProfitPercent);
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
