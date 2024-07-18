using Xpense.Services.Abstract.Models;

namespace Xpense.Services.Models
{
    public class Merchant : IOption<Entities.Merchant>
    {
        public int? Id { get; set; }
        public required string Label { get; set; }
        public bool Create { get; set; }

        public Entities.Merchant ToEntity()
        {
            return new Entities.Merchant
            {
                Label = this.Label
            };
        }
    }
}
