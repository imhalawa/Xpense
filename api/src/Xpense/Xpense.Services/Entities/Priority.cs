namespace Xpense.Services.Entities
{
    public class Priority : BaseEntity
    {
        public string Label { get; set; }
        public double Weight { get; set; }
        public virtual ICollection<Category> Categories { get; set; }
    }
}
