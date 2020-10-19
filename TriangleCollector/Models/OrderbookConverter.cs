using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public class OrderbookConverter : JsonConverter<Orderbook>
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
        public override Orderbook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var ob = new Orderbook();
            var orders = new ConcurrentDictionary<decimal, decimal>();
            bool ask = true;
            decimal lastPrice = 0;

            string currentProperty = string.Empty;

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
                            ob.symbol = reader.GetString();
                        }
                        else if (currentProperty == "timestamp")
                        {
                            ob.timestamp = reader.GetDateTime();
                        }
                        else if (currentProperty == "method")
                        {
                            ob.method = reader.GetString();
                        }
                    }
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    ask = currentProperty == "ask" ? true : false;
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    if (currentProperty == "sequence")
                    {
                        ob.sequence = reader.GetInt32();
                    }
                }
                else if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ask)
                    {
                        ob.officialAsks = orders;
                        orders = new ConcurrentDictionary<decimal, decimal>();
                    }
                    else
                    {
                        ob.officialBids = orders;
                        orders = new ConcurrentDictionary<decimal, decimal>();
                    }
                }
            }

            return ob;
        }

        /// <summary>
        /// Serializing orderbooks to json is not implemented yet
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public override void Write(Utf8JsonWriter writer, Orderbook value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
