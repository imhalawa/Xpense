namespace Xpense.Domain.Entities
{
    public class BaseEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// When Xpense wrote this row. Never set from a request -- a client says when the money
        /// moved, which is <see cref="Transaction.OccurredAt"/>.
        /// </summary>
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
