FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["TriangleCollector/TriangleCollector.csproj", "TriangleCollector/"]

RUN dotnet restore "TriangleCollector/TriangleCollector.csproj"
COPY . .
WORKDIR "/src/TriangleCollector"
RUN dotnet build "TriangleCollector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TriangleCollector.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TriangleCollector.dll"]