namespace Xpense.API.Models
{
    public class PaginatedResponse
    {
        private PaginatedResponse(int statusCode, object data, int page, int pages, int size)
        {
            StatusCode = statusCode;
            Page = page;
            Pages = pages;
            Size = size;
            Data = data;
        }

        public int StatusCode { get; set; }
        public object Data { get; set; }
        public int Page { get; set; }
        public int Pages { get; set; }
        public int Size { get; set; }

        public static PaginatedResponse Ok(object data, int page, int pages, int size)
        {
            return new PaginatedResponse(200, data, page, pages, size);
        }
    }
}
