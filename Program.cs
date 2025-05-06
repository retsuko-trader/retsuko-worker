using Binance.Net.Enums;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var subscriber = new Subscriber();
await subscriber.Init();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/subscribe", async (SubscriptionReq req) => {
  await subscriber.Subscribe(req.id, req.symbol, (KlineInterval)req.interval);
  Console.WriteLine($"Subscribed {req.id} to {req.symbol} with interval {req.interval}");
  return Results.Ok();
});

app.MapPost("/subscribe/{id}/reload", async (string id, [FromBody]SubscriptionReloadReq req) => {
  await subscriber.Reload(id, req.count);
  Console.WriteLine($"Manually reloaded {id} for {req.count} klines");
  return Results.Ok();
});

app.MapDelete("/subscribe/{id}", async (string id) => {
  await subscriber.Unsubscribe(id);
  Console.WriteLine($"Unsubscribed {id}");
  return Results.Ok();
});

app.Run();

record SubscriptionReq(string id, string symbol, int interval);

record SubscriptionReloadReq(int count);
