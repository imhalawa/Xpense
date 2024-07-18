namespace Xpense.Services.Abstract.Entities
{
    public class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool IsDeleted { get; set; }

        public void MarkAsDeleted()
        {
            IsDeleted = true;
        }

        public void Touch()
        {
            LastUpdated = DateTime.Now;
        }
    }
}
