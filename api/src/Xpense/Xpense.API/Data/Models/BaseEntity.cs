using System;

namespace Xpense.API.Data.Models
{
    public class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool IsDeleted { get; set; }
    }
}
