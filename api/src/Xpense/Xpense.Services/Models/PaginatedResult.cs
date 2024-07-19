using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Models
{
    public class PaginatedResult<T>(int page, int size, int pages, IEnumerable<T> data) where T : BaseEntity
    {
        public int Pages { get; set; } = pages;
        public int Size { get; set; } = size;
        public int Page { get; set; } = page;
        public IEnumerable<T> Data { get; set; } = data;

        public static PaginatedResult<T> FromResult(int page, int size, int pages, IEnumerable<T> data) =>
            new(page, size, pages, data);
    }
}
