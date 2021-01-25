using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TriangleCollector.Models.Exchanges.Binance
{
    public class BinanceConverter : JsonConverter<BinanceOrderbook>
    {

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.GetTypeInfo().IsClass;
        }


        /// <summary>
        /// Converts orderbook snapshots and updates to Orderbook objects.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="typeToConvert"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override BinanceOrderbook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var ob = new BinanceOrderbook();
            decimal bestBidPrice = 0m;
            decimal bestBidSize = 0m;
            decimal bestAskPrice = 0m;
            decimal bestAskSize = 0m;

            string currentProperty = string.Empty;
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    currentProperty = reader.GetString();
                }
                else if (reader.TokenType == JsonTokenType.Number && currentProperty == "u")
                {
                    ob.Sequence = reader.GetInt64();
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    if (currentProperty == "s")
                    {
                        ob.Symbol = reader.GetString();
                    }
                    else if (currentProperty == "b")
                    {
                        bestBidPrice = Convert.ToDecimal(reader.GetString());
                    }
                    else if (currentProperty == "B") 
                    {
                        bestBidSize = Convert.ToDecimal(reader.GetString());
                    }
                    else if (currentProperty == "a")
                    {
                        bestAskPrice = Convert.ToDecimal(reader.GetString());
                    }
                    else if (currentProperty == "A")
                    {
                        bestAskSize = Convert.ToDecimal(reader.GetString());
                    }
                }
            }
            ob.OfficialAsks.TryAdd(bestAskPrice, bestAskSize);
            ob.OfficialBids.TryAdd(bestBidPrice, bestBidSize);
            ob.Timestamp = DateTime.UtcNow;
            return ob;
        }

        /// <summary>
        /// Serializing orderbooks to json is not implemented yet
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public override void Write(Utf8JsonWriter writer, BinanceOrderbook value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
