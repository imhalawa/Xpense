using Xpense.Services.Models;

namespace Xpense.Services.Exceptions
{
    public class InvalidFilteredResultParams(FilterQuery query, Exception? innerException = null)
        : XpenseException($"Invalid filtration params {nameof(query.Page)}:{query.Page}, {nameof(query.Size)}:{query.Size} must be greater than 0", innerException);
}
