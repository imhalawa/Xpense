namespace Xpense.API.Data.Models
{
    public class Currency : BaseEntity
    {
        public string Name { get; set; }
        public string ISOCode { get; set; }
        public Country Country { get; set; }

        // From Currency Only
        public ICollection<CurrencyExchangeRate> ExchangeRates { get; set; }
    }
}
