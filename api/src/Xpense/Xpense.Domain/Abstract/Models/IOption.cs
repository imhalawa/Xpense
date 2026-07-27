using Xpense.Domain.Abstract.Entities;

namespace Xpense.Domain.Abstract.Models
{
    public interface IOption<T> where T : IOptionEntity
    {
        int? Id { get; set; }
        string Label { get; set; }
        bool Create { get; set; }

        T ToEntity();
    }
}
