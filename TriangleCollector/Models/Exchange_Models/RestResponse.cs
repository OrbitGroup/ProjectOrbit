using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;

namespace TriangleCollector.Models
{
    public class RestResponse
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

        public static JsonElement.ArrayEnumerator GetSymbolResponse()
        {
            var httpClient = new HttpClient();
            var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync("https://api.hitbtc.com/api/2/public/symbol").Result).Result.RootElement.EnumerateArray();
            httpClient.Dispose();
            return symbols;
        }
    }

}
