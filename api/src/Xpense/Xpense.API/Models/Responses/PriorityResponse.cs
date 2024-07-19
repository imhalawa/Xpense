using System;
using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses
{
    public class PriorityResponse(int id, string label, double weight, long? createdOn, long? lastUpdated)
    {
        public int Id { get; set; } = id;
        public string Label { get; set; } = label;
        public double Weight { get; set; } = weight;
        public long? CreatedOn { get; set; } = createdOn;
        public long? LastUpdated { get; set; } = lastUpdated;

        public static PriorityResponse Of(Priority priority)
        {
            var createdOn = new DateTimeOffset(priority.CreatedOn).ToUnixTimeSeconds();
            long? lastUpdated = priority.LastUpdated.HasValue
                ? new DateTimeOffset(priority.LastUpdated.Value).ToUnixTimeSeconds()
                : null;
            return new(priority.Id, priority.Label, priority.Weight, createdOn, lastUpdated);
        }
    }
}
