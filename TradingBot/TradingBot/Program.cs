using TradingBot.Core;

var host = Host.CreateDefaultBuilder(args)
	.ConfigureServices((hostContext, services) =>
	{
		services.AddHttpClient();
		services.AddHostedService<Worker>();
	})
	.Build();

await host.RunAsync();
