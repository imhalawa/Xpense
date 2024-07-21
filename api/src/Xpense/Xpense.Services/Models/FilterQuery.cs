namespace Xpense.Services.Models
{
    public class FilterQuery(int page, int size, long? date = null)
    {
        public int Size { get; set; } = size;
        public int Page { get; set; } = page;
        public long? date { get; set; } = date;

        public bool IsValid()
        {
            return Page > 0 && Size > 0;
        }

        public static FilterQuery Of(int page, int size, long? date = null) => new(page, size, date);
    }
}
