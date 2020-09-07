using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;

namespace TriangleCollector.Models
{
    public class RestAPIResponse
    {
        [JsonPropertyName("id")]
        public string id { get; set; }
        [JsonPropertyName("baseCurrency")]
        public string baseCurrency { get; set; }
        [JsonPropertyName("quoteCurrency")]
        public string quoteCurrency { get; set; }
        [JsonPropertyName("quantityIncrement")]
        public decimal quantityIncrement { get; set; }
        [JsonPropertyName("tickSize")]
        public decimal tickSize { get; set; }
        [JsonPropertyName("takeLiquidityRate")]
        public decimal takeLiquidityRate { get; set; }
        [JsonPropertyName("provideLiquidityRate")]
        public decimal provideLiquidityRate { get; set; }
        [JsonPropertyName("feeCurrency")]
        public string feeCurrency { get; set; }
        
        
        
        public static JsonElement.ArrayEnumerator TestSymbolResponse()
        {

            RestAPIResponse ETHBTC = new RestAPIResponse
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

            RestAPIResponse EOSETH = new RestAPIResponse
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
            RestAPIResponse EOSBTC = new RestAPIResponse
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
            RestAPIResponse EOSUSD = new RestAPIResponse
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
            RestAPIResponse BTCUSD = new RestAPIResponse
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
            RestAPIResponse LTCBTC = new RestAPIResponse
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


            List<RestAPIResponse> Symbols = new List<RestAPIResponse>();
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

        public static JsonElement.ArrayEnumerator ActualSymbolResponse()
        {
            var httpClient = new HttpClient();
            var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync("https://api.hitbtc.com/api/2/public/symbol").Result).Result.RootElement.EnumerateArray();
            httpClient.Dispose();
            return symbols;
        }
    }

}
