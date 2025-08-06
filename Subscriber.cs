using System.Text.Json;
using Binance.Net.Clients;
using Binance.Net.Enums;
using StackExchange.Redis;

// BinanceStreamKlineData
using Kline = Binance.Net.Objects.Models.Spot.Socket.BinanceStreamKline;

public class Subscriber {
  record Subscription(string symbol, KlineInterval interval, Kline? lastKline, CancellationTokenSource cts);

  record SubscriptionState(string symbol, KlineInterval interval) {}

  private readonly IDatabase db;
  private readonly BinanceSocketClient client = new();
  private readonly BinanceRestClient restClient = new();
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

    await db.HashSetAsync("worker:store", id, JsonSerializer.Serialize(new SubscriptionState(symbol, interval)));
  }

  private async Task LoadSubscription() {
    var entries = await db.HashGetAllAsync("worker:store");

    foreach (var entry in entries) {
      var id = entry.Name.ToString();
      var state = JsonSerializer.Deserialize<SubscriptionState>(entry.Value.ToString())!;
      var subscription = new Subscription(state.symbol, state.interval, null, new CancellationTokenSource());

      Console.WriteLine($"Loading subscription {id} for {state.symbol} with interval {state.interval}");

      await SubscribeInner(id, subscription);
    }
  }

  private async Task SubscribeInner(string id, Subscription subscription) {
    subscriptions[id] = subscription;
    await client.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
      subscription.symbol,
      subscription.interval,
      async x => await OnData(id, (Kline)x.Data.Data),
      false,
      subscription.cts.Token
    );
  }

  public async Task Unsubscribe(string id) {
    if (subscriptions.TryGetValue(id, out var subscription)) {
      subscription.cts.Cancel();
      subscriptions.Remove(id);

      await db.HashDeleteAsync("worker:store", id);
    }
  }

  public async Task Reload(string id, int count) {
    if (!subscriptions.TryGetValue(id, out var subscription)) {
      Console.WriteLine($"Subscription {id} not found");
      return;
    }

    var klines = await restClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(
      subscription.symbol,
      subscription.interval,
      limit: count
    );

    if (!klines.Success) {
      Console.WriteLine($"Failed to reload {id}: {klines.Error}");
    }

    foreach (var kline in klines.Data) {
      await OnData(id, new Kline {
        OpenTime = kline.OpenTime,
        CloseTime = kline.CloseTime,
        OpenPrice = kline.OpenPrice,
        ClosePrice = kline.ClosePrice,
        Volume = kline.Volume
      });
    }
  }

  private async Task OnData(string id, Kline kline) {
    if (!subscriptions.TryGetValue(id, out var subscription)) {
      return;
    }

    var next = subscriptions[id] with { lastKline = kline };
    if (subscription.lastKline == null) {
      subscriptions[id] = next;
      return;
    }

    var symbol = subscription.symbol;
    var interval = subscription.interval;

    if (subscription.lastKline.OpenTime < kline.OpenTime) {
      var k = subscription.lastKline;
      Console.WriteLine($"[{id}] {k.Interval} {k.OpenTime} {k.CloseTime} {k.OpenPrice} {k.ClosePrice} {k.Volume}");

      await db.ListLeftPushAsync("worker:queue", JsonSerializer.Serialize(new { id, symbol, interval, kline = k }));

      var url = Environment.GetEnvironmentVariable("CALLBACK_URL");
      var client = new HttpClient();
      _ = Task.Run(() => client.PostAsJsonAsync(url, new {}));
    }

    subscriptions[id] = next;
  }
}
