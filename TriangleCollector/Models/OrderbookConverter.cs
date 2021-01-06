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

            var firstLine = string.Empty; //the first line will determine which exchange the JSON response is from
            if(reader.Read() && reader.TokenType != JsonTokenType.StartObject)
            {
                firstLine = reader.GetString();
                //Console.WriteLine($"first line is {firstLine}");
            }
            string currentProperty = string.Empty;
            
            if(firstLine == "jsonrpc") //hitbtc
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
                                ob.symbol = reader.GetString();
                            }
                            else if (currentProperty == "timestamp")
                            {
                                ob.timestamp = reader.GetDateTime();
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
                            ob.sequence = reader.GetInt64();
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
                //Console.WriteLine($"{ob.symbol}, {ob.sequence}, {ob.officialAsks.Count()}, {ob.officialBids.Count()}");
                return ob;


            } else if (firstLine == "e" || firstLine == "result") //binance
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        currentProperty = reader.GetString();
                    }
                    else if (reader.TokenType == JsonTokenType.Number && currentProperty == "u")
                    {
                        ob.sequence = reader.GetInt64();
                    }
                    else if (reader.TokenType == JsonTokenType.String)
                    {
                        if (currentProperty == "s")
                        {
                            ob.symbol = reader.GetString();
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            reader.Read();
                        }
                        var layers = new List<decimal>();
                        while (reader.TokenType != JsonTokenType.EndArray && reader.TokenType != JsonTokenType.StartArray)
                        {
                            var price = Convert.ToDecimal(reader.GetString());
                            reader.Read();
                            var size = Convert.ToDecimal(reader.GetString());
                            if (currentProperty == "a")
                            {
                                ob.officialAsks.TryAdd(price, size);
                            }
                            else if (currentProperty == "b")
                            {
                                ob.officialBids.TryAdd(price, size);
                            }
                            reader.Read();
                        }
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
        public override void Write(Utf8JsonWriter writer, Orderbook value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
