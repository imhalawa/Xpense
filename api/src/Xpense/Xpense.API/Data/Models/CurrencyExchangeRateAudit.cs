using System.Collections.Generic;

namespace Xpense.API.Data.Models;

public class CurrencyExchangeRateAudit: BaseEntity
{
    public decimal Rate { get; set; }
    public int PrincipalCurrencyId { get; set; }
    public Currency PrincipalCurrency { get; set; }
    public int ForeignCurrencyId { get; set; }
    public Currency ForeignCurrency { get; set; }
    public int ExchangeRateId { get; set; }
    public CurrencyExchangeRate ExchangeRate { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
}