
namespace Xpense.API.Models;

public class Response<T>
{
    protected Response(int statusCode, T data)
    {
        StatusCode = statusCode;
        Data = data;
    }

    public int StatusCode { get; set; }
    public T Data { get; set; }


    public static Response<T> Ok(T data)
    {
        return new Response<T>(200, data);
    }

    public static Response<T> NotFound(T data)
    {
        return new Response<T>(404, data);
    }

    public static Response<T> BadRequest(T data)
    {
        return new Response<T>(400, data);
    }

    public static Response<T> Problem(T data)
    {
        return new Response<T>(500, data);
    }

    public static Response<T> Of(int statusCode, T data)
    {
        return new Response<T>(statusCode, data);
    }
}