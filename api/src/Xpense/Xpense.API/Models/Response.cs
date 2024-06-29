namespace Xpense.API.Models;

public class Response
{
    public int StatusCode { get; set; }
    public object Data { get; set; }

    private Response(int status, object data)
    {
        this.StatusCode = status;
        this.Data = data;
    }

    public static Response Ok(object data)
    {
        return new Response(200, data);
    }

    public static Response NotFound(object data)
    {
        return new Response(404, data);
    }

    public static Response BadRequest(object data)
    {
        return new Response(400, data);
    }

    public static Response Problem(object error)
    {
        return new Response(500, error);
    }
}