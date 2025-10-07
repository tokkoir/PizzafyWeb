namespace PizzafyWeb.Models
{
    public class CartSettings
    {
        /// <summary>
        /// How to handle price changes: "Update", "Remove", "RemoveOnIncrease"
        /// </summary>
        public string HandlePriceChanges { get; set; } = "Update";
        
        /// <summary>
        /// Whether to remove items when price increases
        /// </summary>
        public bool RemoveOnPriceIncrease { get; set; } = false;
        
        /// <summary>
        /// Price increase threshold (percentage) before removing items
        /// </summary>
        public decimal PriceIncreaseThreshold { get; set; } = 0.20m;
    }
}