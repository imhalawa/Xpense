namespace Xpense.Domain.Entities
{
    public class BaseEntity
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        public void MarkAsDeleted()
        {
            IsDeleted = true;
        }

        public void Touch()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
