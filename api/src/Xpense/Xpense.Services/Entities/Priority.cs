using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Entities
{
    public class Priority : BaseEntity, IEquatable<Priority>
    {
        public required string Label { get; set; }
        public double Weight { get; set; }
        public virtual ICollection<Category>? Categories { get; set; }

        public bool Equals(Priority? other)
        {
            if (other == null) return false;
            return this.Label == other.Label;
        }
    }
}
