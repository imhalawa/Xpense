using System.Collections.Generic;

namespace Xpense.API.Data.Models;

public class CurrencyExchangeRate : BaseEntity
{
    public decimal Rate { get; set; }
    public int PrincipalCurrencyId { get; set; }
    public Currency PrincipalCurrency { get; set; }
    public int ForeignCurrencyId { get; set; }
    public Currency ForeignCurrency { get; set; }
    public virtual ICollection<CurrencyExchangeRateAudit> Audits { get; set; }
}