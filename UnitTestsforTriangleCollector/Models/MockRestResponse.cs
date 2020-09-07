using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TriangleCollector.Models;

namespace TriangleCollector.UnitTests.Models
{
    class MockRestResponse
    {
        public static JsonElement.ArrayEnumerator GetTestSymbolResponse()
        {

            RestResponse ETHBTC = new RestResponse
            {
                id = "ETHBTC",
                baseCurrency = "ETH",
                quoteCurrency = "BTC",
                quantityIncrement = 0.0001m,
                tickSize = 0.000001m,
                takeLiquidityRate = 0.0025m,
                provideLiquidityRate = 0.001m,
                feeCurrency = "BTC"
            };

            RestResponse EOSETH = new RestResponse
            {
                id = "EOSETH",
                baseCurrency = "EOS",
                quoteCurrency = "ETH",
                quantityIncrement = 0.01m,
                tickSize = 0.0000001m,
                takeLiquidityRate = 0.0025m,
                provideLiquidityRate = 0.001m,
                feeCurrency = "ETH"
            };
            RestResponse EOSBTC = new RestResponse
            {
                id = "EOSBTC",
                baseCurrency = "EOS",
                quoteCurrency = "BTC",
                quantityIncrement = 0.01m,
                tickSize = 0.00000001m,
                takeLiquidityRate = 0.0025m,
                provideLiquidityRate = 0.001m,
                feeCurrency = "BTC"
            };
            RestResponse EOSUSD = new RestResponse
            {
                id = "EOSUSD",
                baseCurrency = "EOS",
                quoteCurrency = "USD",
                quantityIncrement = 0.01m,
                tickSize = 0.00001m,
                takeLiquidityRate = 0.0025m,
                provideLiquidityRate = 0.001m,
                feeCurrency = "USD"
            };
            RestResponse BTCUSD = new RestResponse
            {
                id = "BTCUSD",
                baseCurrency = "BTC",
                quoteCurrency = "USD",
                quantityIncrement = 0.00001m,
                tickSize = 0.01m,
                takeLiquidityRate = 0.0025m,
                provideLiquidityRate = 0.001m,
                feeCurrency = "USD"
            };
            RestResponse LTCBTC = new RestResponse
            {
                id = "LTCBTC",
                baseCurrency = "LTC",
                quoteCurrency = "BTC",
                quantityIncrement = 0.001m,
                tickSize = 0000001m,
                takeLiquidityRate = 0.0025m,
                provideLiquidityRate = 0.001m,
                feeCurrency = "BTC"
            };


            List<RestResponse> Symbols = new List<RestResponse>();
            Symbols.Add(EOSBTC);
            Symbols.Add(EOSETH);
            Symbols.Add(ETHBTC);
            Symbols.Add(EOSUSD);
            Symbols.Add(BTCUSD);
            Symbols.Add(LTCBTC);

            var s = JsonSerializer.Serialize(Symbols);
            var apiResponse = JsonDocument.Parse(s).RootElement.EnumerateArray();
            return apiResponse;
        }
    }
}
