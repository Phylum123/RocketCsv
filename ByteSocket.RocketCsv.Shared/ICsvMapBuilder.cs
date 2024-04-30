using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Shared
{
    public interface ICsvMapBuilder<T>
    {
        //public ICsvMapBuilder<T> RowDelimiters(string[] rowDelimiters); //TODO: Move this

        ICsvMapBuilder<T> HeaderDelimiters(string[] columnDelimiters);
        ICsvMapBuilder<T> ColumnDelimiters(string[] columnDelimiters);
        ICsvMapBuilder<T> ColumnTrimChars(char[] trimChars);
        //public ICsvMapBuilder<T> IncludeProperty(string propertyName); //TODO: Fuuture - Use this to include private/internal/etc properties that wouldn't normally be picked up. Users can use "nameof" to avoid naming errors. Can't use "Expression<Func<" because it's not a public property.
        IColumnMapBuilder<T> MapToColumns();

        //public IAutoMapBuilder<T> AutoMapByName(bool ignoreCase = false); //TODO:
        //public IAutoMapBuilder<T> AutoMapByIndex(); //TODO:
    }
    public interface IAutoMapBuilder<T>
    {
        IAutoMapBuilder<T> IgnoreProperty(Expression<Func<T, object>> property);
        IAutoMapBuilder<T> MapToColumnOverride(Expression<Func<T, object>> property, int csvIndex);
        IAutoMapBuilder<T> MapToColumnOverride(Expression<Func<T, object>> property, string csvColumnName);

        //TODO: Add MapColumn by name and index, that has a FUNC parameter for customizing the mapping?
    }

    public interface IColumnMapBuilder<T>
    {
        IColumnMapBuilder<T> MapToColumn(Expression<Func<T, object>> property, int csvIndex);
        IColumnMapBuilder<T> MapToColumn(Expression<Func<T, object>> property, string csvColumnName);

        //TODO: Add MapColumn by name and index, that has a FUNC parameter for customizing the mapping?
    }

}
