# TriangleCollector

TriangleCollector is a containerized .NET Core app which determines all possible [triangular arbitrage opportunities](https://www.investopedia.com/terms/t/triangulararbitrage.asp) in cryptocurrency markets. It accomplishses this by establishing websocket API connections subscribing to all relevant orderbooks via the exchange's API, and calculates all possible triangular arbitrage opportunities whenever an update is received via the exchange's websocket. Triangular arbitrage opportunities are sorted by profitability and the maximum liquidity available (as a market taker) is calculated as well.

Currently the application initiates and maintains websocket connections for 1,949 markets, which interact with each other to form 2,070 possible triangular arbitrage opportunities. In receiving real-time orderbook updates to 1,949 markets via the exchange's websocket API, this yields thousands of triangular arbitrage opportunities to calculate per second. 

This is very much a work-in-progress and there is still a lot to be done, including storing market data and triangular arbitrage oppportunies in a database, and publishing this data via a front-end solution.

## Development

Development should be fully supported on Linux/Windows/Mac as the project does not take a dependency on anything other than native .NET Core libraries. 

All you need to be able to run this project is .NET Core 3.1 and an IDE. You do not need to have Docker installed. Just load the solution and try to run it, your IDE (Visual Studio, Rider, etc) should automatically restore the nuget packages like magic.

## Models

The models consist of Orderbook, Triangle, and OrderbookConverter. The only real things of note here are:

- The 'orderbook' model contains the logic for merging orderbook updates, and stores all of the bid and ask orders (the equivalent of [Level 2 Data](https://www.investopedia.com/articles/trading/06/level2quotes.asp)).  

- The 'triangle' model contains the logic for calculating the profitability of a triangular arbitrage opportunity as well as the liquidity available in the market. 

- OrderbookConverter is used for json deserialization of orderbook snapshots and updates. Although the code isn't perfect, it's very fast and typically deserialization takes somewhere in the range of 1-5ms.

## Services

Services are the construct for the [BackgroundService](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1) abstraction in .NET Core. There are 6 services that are running mostly simultaneously in TriangleCollector, with the exception being OrderbookSubscriber which right now is not running continuously.

### Orderbook Subscriber

This service gets a list of all markets from the exchange via their REST API, then determines all possible trianglular arbitrage opportunities by matching up the base and quote currencies of all markets, then sends the subscription message to the exchange websocket API to subscribe to each relevant orderbook. The subscriber creates a new ClientWebSocket and OrderbookListener per group of symbols as determined in the subscriber service (currently 50 symbols per ClientWebSocket).

### Orderbook Listener

Orderbook Listeners are created by the OrderbookSubscriber. OrderbookSubscriber is what sets the number markets that a given ClientWebSocket can have. ClientWebSockets and Orderbook Listeners are 1:1. If the max markets in the subscriber is set to 50, and you have 200 markets, you will end up with 4 ClientWebSockets and 4 Orderbook Listeners with 50 markets each.

This listens for orderbook updates and merges them into the corresponding "official" orderbook for that market. Official in this codebase simply refers to the one that the TriangleCalculator treats as the real orderbook.

Once an update comes in and has been merged into the official orderbook, the impacted triangles are put in the TrianglesToRecalculate queue.

### TriangleCalculator

TriangleCalculator is listening to the TrianglesToRecalculate queue and then grabbing all 3 orderbooks, updating the Triangle objects' ask/bid prices for each market, and then recalculating profitability and volume. Triangles in the Triangles dictionary are updated and refresh times are updated in the TriangleRefreshTimes dictionary.

### QueueMonitor

QueueMonitor monitors the size of the TrianglesToRecalculate queue and if the size exceeds a set limit will create additional TriangleCalculator instances. It also logs various statistics about queue sizes, refresh times, etc.

### TrianglePublisher

TrianglePublisher is used to publish the Triangles dictionary to redis and publish a message to redis for subscribers (the work-in-progress [frontend signalr api](https://github.com/OrbitGroup/TriArbAPI)) to get newly updated triangles.
