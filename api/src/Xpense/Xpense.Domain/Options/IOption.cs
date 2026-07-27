using Xpense.Domain.Entities;

namespace Xpense.Domain.Options
{
    public interface IOption<T> where T : IOptionEntity
    {
        int? Id { get; set; }
        string Label { get; set; }
        bool Create { get; set; }

        T ToEntity();
    }
}
