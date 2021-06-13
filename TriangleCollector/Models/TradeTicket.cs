using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Models
{
    public class TradeTicket
    {
        public IExchange Exchange { get; set; }

        public IOrderbook Market { get; set; }    

        public DateTime TimeTicketCreated { get; set; }

        public DateTime TimeOrderSubmitted { get; set; }

        public DateTime TimeOrderExecuted { get; set; }

        public OrderTypes OrderType { get; set; }

        public OrderDirections OrderDirection { get; set; }

        public OrderStatuses OrderStatus { get; set; }

        public enum OrderStatuses
        {
            Submitted,
            Filled,
            PartiallyFilled,
            Cancelled
        }

        public enum OrderTypes
        {
            Market,
            Limit,
            Stop
        }

        public enum OrderDirections
        {
            Buy,
            Sell
        }

        public decimal DesiredPrice { get; set; }

        public decimal FilledPrice { get; set; }

        public decimal DesiredQuantity { get; set; }

        public decimal FilledQuantity { get; set; }


        public TradeTicket(IOrderbook orderbook, OrderTypes type, OrderDirections direction, decimal desiredPrice, decimal desiredQuantity) //limit or stop order
        {
            TimeTicketCreated = DateTime.UtcNow;
            Market = orderbook;
            Exchange = Market.Exchange;
            OrderType = type;
            OrderDirection = direction;
            DesiredPrice = desiredPrice;
            DesiredQuantity = desiredQuantity;
        }
        public TradeTicket(IOrderbook orderbook, OrderTypes type, OrderDirections direction, decimal desiredQuantity) //market order (no need to input a price)
        {
            TimeTicketCreated = DateTime.UtcNow;
            Market = orderbook;
            Exchange = Market.Exchange;
            OrderType = type;
            OrderDirection = direction;
            DesiredQuantity = desiredQuantity;
        }
    }
}
