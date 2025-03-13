using Binance.Net.Clients;
using Binance.Net.Enums;
using StackExchange.Redis;

public class Subscriber {
  record Subscription(string symbol, KlineInterval interval, CancellationTokenSource cts);

  private IDatabase db;
  private BinanceSocketClient client = new();
  private Dictionary<string, Subscription> subscriptions = [];

  public async Task Init() {
    var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions {
      Password = Environment.GetEnvironmentVariable("REDIS_PASSWORD"),
      EndPoints = { $"{Environment.GetEnvironmentVariable("REDIS_HOST")}:{Environment.GetEnvironmentVariable("REDIS_PORT")}" },
    });

    db = redis.GetDatabase();
  }
}