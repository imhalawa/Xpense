using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Models
{
    public class PaginatedResult<T>(int page, int size, int pages, int totalItems, IEnumerable<T> data) where T : BaseEntity
    {
        public int Pages { get; set; } = pages;
        public int Size { get; set; } = size;
        public int Page { get; set; } = page;
        public int TotalItems { get; set; } = totalItems;
        public IEnumerable<T> Data { get; set; } = data;

        public static PaginatedResult<T> FromResult(int page, int size, int pages, int totalItems, IEnumerable<T> data) =>
            new(page, size, pages, totalItems, data);
    }
}
