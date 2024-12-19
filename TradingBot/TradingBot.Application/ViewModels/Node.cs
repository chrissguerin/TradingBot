namespace TradingBot.Application.ViewModels
{
	public class Node
	{
		public string Id { get; set; }
		public double MarkupPercent { get; set; }
		public List<TradeItems> TradeItems { get; set; }

	}
}
