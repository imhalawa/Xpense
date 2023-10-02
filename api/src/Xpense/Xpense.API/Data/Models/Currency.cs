namespace Xpense.API.Data.Models
{
    public class Currency : BaseEntity
    {
        public string Name { get; set; }
        public string IsoCode { get; set; }
        public virtual ICollection<Country> Countries { get; set; }
        public virtual ICollection<CurrencyExchangeRate> PrincipalExchangeRates { get; set; }
        public virtual ICollection<CurrencyExchangeRate> ForeignCurrencyExchangeRates{ get; set; }
        public virtual ICollection<CurrencyExchangeRateAudit> PrincipalExchangeRateAudits { get; set; }
        public virtual ICollection<CurrencyExchangeRateAudit> ForeignCurrencyExchangeRateAudits { get; set; }

        public virtual ICollection<Account> LinkedAccounts { get; set; }
    }
}
