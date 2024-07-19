namespace Xpense.Services.Models
{
    public class FilterQuery(int page, int size)
    {
        public int Size { get; set; } = size;
        public int Page { get; set; } = page;

        public bool IsValid()
        {
            return Page > 0 && Size > 0;
        }

        public static FilterQuery Of(int page, int size) => new(page, size);
    }
}
