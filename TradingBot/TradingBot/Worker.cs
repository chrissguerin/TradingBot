using GraphQL.Common.Response;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Text;
using TradingBot.Application.ViewModels;

namespace TradingBot.Core
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly string _url = "https://api.csgoroll.com/graphql";
		private readonly string _operationName = "TradeList";
		private readonly string _persistedQueryHash = "0f3a1ea7529016eaa9d8daee8fa24661e437d1a80d81670003b9484dc47bcb4c";

		// Replace with your actual API token if required
		private readonly string _apiToken = Environment.GetEnvironmentVariable("CSGOROLL_API_TOKEN");

		// Define the persisted query structure
		private readonly PersistedQuery _persistedQuery = new PersistedQuery
		{
			Version = 1,
			Sha256Hash = "0f3a1ea7529016eaa9d8daee8fa24661e437d1a80d81670003b9484dc47bcb4c"
		};

		// Retry policy using Polly
		private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

		public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
		{
			_logger = logger;
			_httpClientFactory = httpClientFactory;

			// Initialize the retry policy: retry 3 times with exponential backoff
			_retryPolicy = Policy
				.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
				.WaitAndRetryAsync(
					3,
					retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
					(response, delay, retryCount, context) =>
					{
						_logger.LogWarning($"Request failed with {response.Result.StatusCode}. Waiting {delay} before next retry. Retry attempt {retryCount}.");
					});
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

			// Ensure console can display Unicode characters
			Console.OutputEncoding = Encoding.UTF8;

			string afterCursor = null;

			while (!stoppingToken.IsCancellationRequested)
			{
				// Construct the payload
				var payload = new
				{
					operationName = _operationName,
					variables = new
					{
						first = 50,
						orderBy = "BEST_DEALS",
						status = "LISTED",
						steamAppName = "CSGO",
						t = "1734586625135",
						after = afterCursor
					},
					extensions = new
					{
						persistedQuery = _persistedQuery
					}
				};

				var jsonPayload = JsonConvert.SerializeObject(payload);

				// Create the HTTP request
				var request = new HttpRequestMessage(HttpMethod.Post, _url);
				request.Headers.Add("User-Agent", "Mozilla/5.0");
				request.Headers.Add("Accept", "application/json");

				if (!string.IsNullOrEmpty(_apiToken))
				{
					request.Headers.Add("Authorization", $"Bearer {_apiToken}");
				}

				request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

				// Get the HttpClient
				var client = _httpClientFactory.CreateClient();

				HttpResponseMessage response = null;

				try
				{
					// Execute the request with retry policy
					response = await _retryPolicy.ExecuteAsync(() => client.SendAsync(request, stoppingToken));
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "An error occurred while sending the request.");
					break;
				}

				if (response.IsSuccessStatusCode)
				{
					string responseContent = await response.Content.ReadAsStringAsync();

					// Deserialize the response
					var graphQLResponse = JsonConvert.DeserializeObject<GraphQLResponse>(responseContent);
					var data = graphQLResponse.GetDataFieldAs<Trades>("trades");

					if (data.Edges != null)
					{
						foreach (var edge in data.Edges)
						{
							var node = edge.Node;

							Console.WriteLine($"Trade ID: {node.Id}");

							foreach (var tradeItem in node.TradeItems)
							{
								var marketName = tradeItem.MarketName;
								var price = tradeItem.Value;
								var priceChange = tradeItem.MarkupPercent;

								Console.WriteLine($" - Skin Name: {marketName}");
								Console.WriteLine($"   Price: {price}");
								Console.WriteLine($"   Price Compared to Market: {priceChange}%");
							}
							Console.WriteLine(); // Add an empty line between trades
						}
					}
					else
					{
						_logger.LogWarning("Unexpected response structure.");
						break;
					}
				}
				else
				{
					_logger.LogError("Request failed with status code {StatusCode}: {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
					break;
				}

				// Optional: Delay between requests to respect rate limits
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
			}

			_logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
		}
	}

	// Define the persisted query structure
	public class PersistedQuery
	{
		[JsonProperty("version")]
		public int Version { get; set; }

		[JsonProperty("sha256Hash")]
		public string Sha256Hash { get; set; }
	}
}
