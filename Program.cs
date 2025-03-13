using Binance.Net.Clients;

var client = new BinanceSocketClient();
Console.WriteLine(Environment.GetEnvironmentVariable("REDIS_PORT"));

var cts = new CancellationTokenSource();
var api = await client.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync("BTCUSDT", Binance.Net.Enums.KlineInterval.OneMinute, x => {
  var data = x.Data.Data;
  Console.WriteLine($"Time: {data.OpenTime}, Open: {data.OpenPrice}, Close: {data.ClosePrice}");
}, false, cts.Token);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

Console.ReadLine();

cts.Cancel();

app.Run();