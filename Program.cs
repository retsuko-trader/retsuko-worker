using Binance.Net.Enums;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var subscriber = new Subscriber();
await subscriber.Init();

var app = builder.Build();

app.MapPost("/subscribe", async (SubscriptionReq req) => {
  await subscriber.Subscribe(req.id, req.symbol, (KlineInterval)req.interval);
  return Results.Ok();
});

app.MapDelete("/subscribe/{id}", async (string id) => {
  await subscriber.Unsubscribe(id);
  return Results.Ok();
});

app.Run();

record SubscriptionReq(string id, string symbol, int interval) {}
