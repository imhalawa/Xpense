using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses
{
    public class MerchantResponse(
        int id,
        string label)
    {
        public int Id { get; set; } = id;
        public string Label { get; set; } = label;
        public bool Create { get; set; } = false;

        public static MerchantResponse Of(Merchant merchant) => new(merchant.Id, merchant.Label);
    }
}
