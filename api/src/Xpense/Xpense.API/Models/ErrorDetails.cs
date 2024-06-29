using System;

namespace Xpense.API.Models;

public class ErrorDetails(string message)
{
    public string Message { get; set; } = message;

    public static ErrorDetails Of(string message)
    {
        return new ErrorDetails(message);
    }

    public static ErrorDetails Of(Exception ex)
    {
        return new ErrorDetails(ex.Message);
    }
}