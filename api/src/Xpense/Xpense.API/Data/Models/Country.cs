namespace Xpense.API.Data.Models;

public class Country : BaseEntity
{
    public string Name { get; set; }
    public string DialCode { get; set; }
    public int CurrencyId { get; set; }
    public Currency Currency { get; set; }
}