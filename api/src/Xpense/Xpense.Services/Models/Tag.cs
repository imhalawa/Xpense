using Xpense.Services.Abstract.Models;

namespace Xpense.Services.Models
{
    public class Tag : IOption<Entities.Tag>
    {
        public int? Id { get; set; }
        public required string Label { get; set; }
        public bool Create { get; set; } = false;

        public Entities.Tag ToEntity()
        {
            return new Entities.Tag
            {
                Label = this.Label,
                CreatedOn = DateTime.Now
            };
        }
        public static IEnumerable<Entities.Tag> ToEntityRange(IEnumerable<Tag> tags)
        {
            return tags.Select(t => t.ToEntity());
        }
    }
}
