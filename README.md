# Project Orbit

Project Orbit is a .NET Core application which analyzes 2,761 unique cryptocurrency markets traded across three exchanges, and calculates every possible [triangular arbitrage opportunity](https://www.investopedia.com/terms/t/triangulararbitrage.asp) as well as the liquidity available as a market taker. It first polls the REST API services for each exchange for listings of all traded markets as well as the best bid and ask levels available. It then uses the base and quote currencies of each market to map every possible triangular arbitrage opportunity, and calculates the profitability of each opportunity as of that moment in time. For markets that interact with opportunities above a profitability threshold, it then initiates websocket subscriptions with the exchange for real-time orderbook updates, and recalculates the impacted triangular arbitrage opportunities whenever the orderbooks change. Depending on the volume and price efficiency of the exchange, as little as 40% or as many as 90% of markets may meet the threshold for websocket subscription.

Once the application has initiated websocket subscriptions for its relevant markets, it will receive and deserialize thousands of JSON messages per second, and will locally maintain real-time orderbooks for all subscribed markets (which are then referenced for calculating triangular arbitrage opportunities).

## Development

Development should be fully supported on Linux/Windows/Mac as the project does not take a dependency on anything other than native .NET Core libraries. 

All you need to be able to run this project is .NET Core 3.1 and an IDE. You do not need to have Docker installed. Simply load the solution and run it, your IDE (Visual Studio, Rider, etc) should automatically restore the nuget packages.

## Models

The models provide interfaces which structure the Exchange, Orderbook, OrderbookConverter, and Triangle objects. 

- The 'Exchange' models implement the IExchange interface and control the overall structure at the exchange level (for example, storing the markets traded on each respective exchange, along with queues and services for receiving market data and calculating trading opportunities. Each Exchange has a Client, Converter, and Orderbook model. The Client model stores the information and methods used to interact with the exchange's REST API and websocket services. The Converter model provides the logic which deserializes and processes market data receipts, and the Orderbook model provides the structure and methods to locally manage the Orderbooks for each market (the equivalent of [Level 2 Data](https://www.investopedia.com/articles/trading/06/level2quotes.asp)).  

- The 'Triangle' model contains the logic for calculating the profitability of a triangular arbitrage opportunity as well as the liquidity available in the market for that opportunity as a market taker. 

## Services

Services are the construct for the [BackgroundService](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1) abstraction in .NET Core. There are 7 services that are running simultaneously in Project Orbit.

### Subscription Manager
Each exchange starts a Subscription Manager service when first initialized. This service polls the REST API for its respective exchange to retrieve all traded markets and the best bid/ask information. It then maps these markets into all possible triangular arbitrage opportunities and determines which markets are eligible for real-time websocket subscriptions. Once these markets are identified, they are queued for subscription via the Orderbook Subscriber service. 

Currently this service runs once upon initialization, but the goal is for it to periodically retrieve and map markets to account for any delisted or newly listed markets, and also to dynamically change its threshold for subscription based on market conditions.

### Orderbook Subscriber

The Orderbook Subscriber interacts with the Subscription Queue for each exchange and will constantly create new websocket clients and subscriptions until the queue is empty. Whenever a new websocket client is created, this service will also initialize a new Orderbook Listener to receive and manage the JSON websocket messages. If the Orderbook Subscriber fails to initiate a connection to the exchange for any reason, it will automatically create a new client and try again. It also monitors the subscription capacity for each Listener object and will create new Listeners as needed. Each exchange has a different per-client capacity and subscription methodology, which we've observed and adapted to over time.

### Orderbook Listener

The Orderbook Listener service buffers the JSON data receipts from exchange websocket connections and constantly reads and deserializes the data into orderbook updates. This service is capable of reading websocket messages that arrive in plain text, or in compressed GZIP format. Once a message is received, the Listener uses the appropriate Orderbook Converter model to deserialize the message into an orderbook, and then will merge the orders in this new orderbook into the locally managed orderbook. For these updates, the quantities noted for each price level dictate the absolute quantity available for sale/purchase at that price level. A quantity of zero indicates that there are no bid/ask orders left at that price level.

Once the orderbook update has been deserialized and merged, this service calls the Significant Change method within the Orderbook object to determine whether the orderbook update is noteworthy enough to merit recalculation of its related triangular arbitrage opportunities. The Significant Change method evaluates whether the market has been profitable in the past, and whether the updated price/size information is favorable (in the context of triangular arbitrage) compared to the last best bid/ask information.

If an update is considered to be significant per the above framework, every possible triangular arbitrage opportunity that interacts with that market is queued for calculation (in the TrianglesToRecalculate queue).

In addition, if a websocket client is disconnected from its respective exchange, the Orderbook Listener will detect this disconnection and will re-queue the markets that were formerly subscribed to that connection for re-subscription.

### Triangle Calculator

The Triangle Calculator constantly pulls from the TrianglesToRecalculate queue and triggers the calculation of each triangular arbitrage opportunity as it is dequeued (all queues in Project Orbit work on a First in, First Out basis). The triangular arbitrage calculation determines the profitability percentage based on the prices of each market, and also calculates the liquidity available for the trade (defined as the smallest amount of liquidity available of the three markets). It even simulates the impact that the trader would have on liquidity available in the orderbooks as a market taker by creating a copy of each orderbook and removing the calculated liquidity at each leg of the trade.

If a triangular arbitrage opportunity is profitable at a price level, and our trade eliminates all liquidity available at a market's price level, the method will continue analyzing the liquidity available at the next price levels for as long as the price levels produce a profitable trade.

### Queue Monitor

The Queue Monitor monitors the size of the TrianglesToRecalculate queue and if the size exceeds a set limit will create additional Triangle Calculator instances. 

### Activity Monitor

The Acitivity Monitor monitors and logs a number of performance and diagnostic metrics for the user, such as the number of websocket messages received per second, the number of triangular arbitrage opportunities calculated per second, and the number of active websocket clients and subscriptions.

### USD Monitor

The USD Monitor initiates a websocket subscription with Bitstamp to receive and store the real-time price updates for BTC/USD, which can then be utilized by other services to translate any BTC denominated metric or outcome into USD terms.

