using System.Text.Json;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using StackExchange.Redis;

// BinanceStreamKlineData
using Kline = Binance.Net.Objects.Models.Spot.Socket.BinanceStreamKline;

public class Subscriber {
  record Subscription(string symbol, KlineInterval interval, Kline? lastKline, CancellationTokenSource cts);

  record SubscriptionState(string symbol, KlineInterval interval, Kline? lastKline) {}

  private readonly IDatabase db;
  private readonly BinanceSocketClient client = new();
  private readonly Dictionary<string, Subscription> subscriptions = [];

  public Subscriber() {
    var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions {
      Password = Environment.GetEnvironmentVariable("REDIS_PASSWORD"),
      EndPoints = { $"{Environment.GetEnvironmentVariable("REDIS_HOST")}:{Environment.GetEnvironmentVariable("REDIS_PORT")}" },
    });

    db = redis.GetDatabase();
  }

  public async Task Init() {
    await LoadSubscription();
  }

  public async Task Subscribe(string id, string symbol, KlineInterval interval) {
    var cts = new CancellationTokenSource();
    var subscription = new Subscription(symbol, interval, null, cts);

    await SubscribeInner(id, subscription);

    await db.HashSetAsync("worker:store", id, JsonSerializer.Serialize(new SubscriptionState(symbol, interval, null)));
  }

  private async Task LoadSubscription() {
    var entries = await db.HashGetAllAsync("worker:store");

    foreach (var entry in entries) {
      var id = entry.Name.ToString();
      var state = JsonSerializer.Deserialize<SubscriptionState>(entry.Value.ToString())!;
      var subscription = new Subscription(state.symbol, state.interval, state.lastKline, new CancellationTokenSource());

      await SubscribeInner(id, subscription);
    }
  }

  private async Task SubscribeInner(string id, Subscription subscription) {
    subscriptions[id] = subscription;
    await client.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(subscription.symbol, subscription.interval, async x => await OnData(id, (Kline)x.Data.Data), false, subscription.cts.Token);
  }

  public async Task Unsubscribe(string id) {
    if (subscriptions.TryGetValue(id, out var subscription)) {
      subscription.cts.Cancel();
      subscriptions.Remove(id);

      await db.HashDeleteAsync("worker:store", id);
    }
  }

  private async Task OnData(string id, Kline kline) {
    var subscription = subscriptions[id];
    var next = subscriptions[id] with { lastKline = kline };
    if (subscription.lastKline == null) {
      subscriptions[id] = next;
      return;
    }

    if (subscription.lastKline.OpenTime < kline.OpenTime) {
      var k = subscription.lastKline;
      Console.WriteLine($"[{id}] {k.Interval} {k.OpenTime} {k.CloseTime} {k.OpenPrice} {k.ClosePrice} {k.Volume}");

      await db.HashSetAsync("worker:store", id, JsonSerializer.Serialize(new SubscriptionState(subscription.symbol, subscription.interval, kline )));
    }


    subscriptions[id] = next;

    await db.ListLeftPushAsync("worker:queue", JsonSerializer.Serialize(new { id, kline }));

    var url = Environment.GetEnvironmentVariable("FETCH_URL");
    var client = new HttpClient();
    _ = Task.Run(() => client.PostAsJsonAsync(url, new { id, kline }));
  }
}
