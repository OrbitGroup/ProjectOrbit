using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TriangleCollector.Models.Exchanges.Huobi
{
    public class HuobiConverter : JsonConverter<HuobiOrderbook>
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
        public override HuobiOrderbook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var ob = new HuobiOrderbook();
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

            if (firstLine == "id" || firstLine == "ch") //huobi
            {
                if (firstLine == "ch")
                {
                    reader.Read();
                    var channel = reader.GetString().Split(".");
                    if (channel.Length > 1)
                    {
                        ob.Symbol = channel[1].ToUpper(); //the second period-delimited item is the symbol
                    }
                    else
                    {
                        Console.WriteLine($"channel only has one element: {channel[0]}");
                    }
                }
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        currentProperty = reader.GetString();
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        reader.Read();
                        while (reader.TokenType != JsonTokenType.EndObject && reader.TokenType != JsonTokenType.StartObject)
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                currentProperty = reader.GetString();
                            }
                            else if (currentProperty == "seqNum")
                            {
                                ob.Sequence = reader.GetInt64();
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
                                    var price = reader.GetDecimal();
                                    reader.Read();
                                    var size = reader.GetDecimal();
                                    if (currentProperty == "asks")
                                    {
                                        ob.OfficialAsks.TryAdd(price, size);
                                        //Console.WriteLine($"converted ask {price} price, {size} size");
                                    }
                                    else if (currentProperty == "bids")
                                    {
                                        ob.OfficialBids.TryAdd(price, size);
                                        //Console.WriteLine($"converted bid {price} price, {size} size");
                                    }
                                    reader.Read();
                                }
                            }
                            reader.Read();
                        }
                    }
                }
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

        public override void Write(Utf8JsonWriter writer, HuobiOrderbook value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
