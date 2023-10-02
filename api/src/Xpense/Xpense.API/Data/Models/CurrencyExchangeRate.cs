namespace Xpense.API.Data.Models;

public class CurrencyExchangeRate : BaseEntity
{
    public Currency FromCurrency { get; set; }
    public Currency ToCurrency { get; set; }
    public decimal Rate { get; set; }
}