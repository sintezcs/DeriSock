// See https://aka.ms/new-console-template for more information

using DeriSock;
using DeriSock.Model;

DotNetEnv.Env.Load();
string deriBitApiKey = Environment.GetEnvironmentVariable("DERIBIT_API_KEY");
string deriBitApiSecret = Environment.GetEnvironmentVariable("DERIBIT_API_SECRET");


Console.WriteLine("Staring DeriSock Console Test...");

if (deriBitApiKey == null || deriBitApiSecret == null)
{
  Console.WriteLine("No DeriBit API Key or Secret");
  return;
}

var deribitclient = new DeribitClient(EndpointType.Productive);
deribitclient.Connect().Wait(10000);

Console.WriteLine("Deribit connected from start private websockket: " + deribitclient.IsConnected);

await deribitclient.Authentication.PublicLogin().WithClientCredentials(deriBitApiKey, deriBitApiSecret);

Console.WriteLine("Successfully authenticated");
// Subscribe to user instrument changes for a specific instrument with raw notification interval
Console.WriteLine("Subscribing to user instrument changes...");
// var subscribe_stream = await deribitclient.Subscriptions.SubscribeUserInstrumentChanges(
//   new UserInstrumentChangesChannel()
//   {
//     InstrumentName = ExampleInstrument, Interval = NotificationInterval2.Raw
//   });
var subscribeStream = await deribitclient.Subscriptions.SubscribeDeribitPriceIndex(
  new DeribitPriceIndexChannel()
  {
    IndexName = IndexName.BtcUsd
  });

Console.WriteLine("Subscribed to user instrument changes.");


// Test function to try breaking the connection
async void TryGetAccountSummary()
{
  Console.WriteLine("Getting account summary...");
  try
  {
    var account = await deribitclient.Private.GetAccountSummary(
      new PrivateGetAccountSummaryRequest() { Currency = CurrencySymbol.BTC });
    Console.WriteLine("Account balance: " + account.Data.Balance);
  }
  catch (InvalidOperationException exception)
  {
    Console.WriteLine(exception.Message);
  }
}


using var CTS = new CancellationTokenSource();

// Schedule the test function to run every 10 seconds
var timer = new System.Timers.Timer(5000);
timer.Elapsed += (sender, e) => TryGetAccountSummary();
timer.Start();

Console.WriteLine("Press return key to cancel");
_ = Task.Run(() =>
{
  Console.ReadLine();
  CTS.Cancel();
});

try
{
  await foreach (var notific in subscribeStream.WithCancellation(CTS.Token))
  {
    var currentTime = DateTimeOffset.UtcNow.ToString("HH:mm:ss");
    Console.WriteLine($"[{currentTime}] ${notific.Data.IndexName}: {notific.Data.Price}");
  }
}
catch (OperationCanceledException)
{
  Console.WriteLine("Cancellation requested.");
}

timer.Stop();
timer.Dispose();
await deribitclient.Disconnect();
Console.WriteLine("Deribit connected: " + deribitclient.IsConnected);
