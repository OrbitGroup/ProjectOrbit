using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TriangleCollector.Models.Exchanges.Hitbtc
{
    public class HitbtcConverter : JsonConverter<HitbtcOrderbook>
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
        public override HitbtcOrderbook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var ob = new HitbtcOrderbook();
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

            if (firstLine == "jsonrpc") //hitbtc
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        currentProperty = reader.GetString();
                    }
                    else if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                    {
                        var value = reader.GetBoolean();
                    }
                    else if (reader.TokenType == JsonTokenType.String)
                    {
                        if (currentProperty == "price")
                        {
                            lastPrice = decimal.Parse(reader.GetString());
                        }
                        else if (currentProperty == "size")
                        {
                            var size = decimal.Parse(reader.GetString());
                            orders.TryAdd(lastPrice, size);
                        }
                        else
                        {
                            if (currentProperty == "symbol")
                            {
                                ob.Symbol = reader.GetString();
                            }
                            else if (currentProperty == "timestamp")
                            {
                                ob.Timestamp = reader.GetDateTime();
                            }
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        if (currentProperty == "ask")
                        {
                            ask = true;
                        }
                        else
                        {
                            ask = false;
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.Number)
                    {
                        if (currentProperty == "sequence")
                        {
                            ob.Sequence = reader.GetInt64();
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        if (ask)
                        {
                            ob.OfficialAsks = orders;
                            orders = new ConcurrentDictionary<decimal, decimal>();
                        }
                        else
                        {
                            ob.OfficialBids = orders;
                            orders = new ConcurrentDictionary<decimal, decimal>();
                        }
                    }
                }

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

        public override void Write(Utf8JsonWriter writer, HitbtcOrderbook value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
