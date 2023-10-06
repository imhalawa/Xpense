namespace Xpense.API.Data.Models;

public class CategoryLevel : BaseEntity
{
    /// <summary>
    ///   <para>
    /// Gets or sets the category level name.
    /// </para>
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; set; }

    /// <summary>Gets or sets the category level weight (1-0) where 1 represents Highest Risk, 0 No Risk.</summary>
    /// <value>The weight.</value>
    public int Weight { get; set; }

    public virtual ICollection<Category> Categories { get; set; }
}