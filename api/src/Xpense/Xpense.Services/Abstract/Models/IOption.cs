using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Abstract.Models
{
    public interface IOption<T> where T : IOptionEntity
    {
        int? Id { get; set; }
        string Label { get; set; }
        bool Create { get; set; }

        T ToEntity();
    }
}
