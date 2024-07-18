using System;
using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses
{
    public class MerchantResponse(
        int id,
        string label,
        long? createdOn,
        long? lastUpdated)
    {
        public int Id { get; set; } = id;
        public string Label { get; set; } = label;
        public bool Create { get; set; } = false;
        public long? CreatedOn { get; set; } = createdOn;
        public long? LastUpdated { get; set; } = lastUpdated;

        public static MerchantResponse Of(Merchant merchant)
        {
            var createdOn = new DateTimeOffset(merchant.CreatedOn).ToUnixTimeSeconds();
            long? lastUpdated = merchant.LastUpdated.HasValue
                ? new DateTimeOffset(merchant.LastUpdated.Value).ToUnixTimeSeconds()
                : null;
            return new MerchantResponse(merchant.Id, merchant.Label, createdOn, lastUpdated);
        }
    }
}
