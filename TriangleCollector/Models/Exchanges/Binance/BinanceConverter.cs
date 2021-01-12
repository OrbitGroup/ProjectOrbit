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
            var orders = new ConcurrentDictionary<decimal, decimal>();
            bool ask = true;
            decimal lastPrice = 0;

            var firstLine = string.Empty; //the first line will determine which exchange the JSON response is from
            if (reader.Read() && reader.TokenType != JsonTokenType.StartObject)
            {
                firstLine = reader.GetString();
                //Console.WriteLine($"first line is {firstLine}");
            }
            string currentProperty = string.Empty;

            
            if (firstLine == "e" || firstLine == "result") //binance
            {
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
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            reader.Read();
                        }
                        while (reader.TokenType != JsonTokenType.EndArray && reader.TokenType != JsonTokenType.StartArray)
                        {
                            var price = Convert.ToDecimal(reader.GetString());
                            reader.Read();
                            var size = Convert.ToDecimal(reader.GetString());
                            if (currentProperty == "a")
                            {
                                ob.OfficialAsks.TryAdd(price, size);
                            }
                            else if (currentProperty == "b")
                            {
                                ob.OfficialBids.TryAdd(price, size);
                            }
                            reader.Read();
                        }
                    }
                }
                ob.Timestamp = DateTime.UtcNow;
                return ob;
            }
            else if (firstLine == "ping")
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        ob.Pong = true;
                        ob.PongValue = reader.GetInt64();
                    }
                }
                return ob;
            }

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
