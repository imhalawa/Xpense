using Xpense.API.Enums;

namespace Xpense.API.Data.Models;

public class Category
{
    public string Name { get; set; }
    public CategoryLevel CategoryLevel { get; set; }

}