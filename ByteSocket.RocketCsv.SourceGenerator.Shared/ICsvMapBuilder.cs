using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.SourceGenerator.Shared
{

    /// <summary>
    /// Represents the behavior when encountering a column parse failure during CSV parsing.
    /// </summary>
    public enum ParseFailureDefault
    {
        ThrowException,
        SkipColumn,
    }
     
    /// <summary>
    /// Represents the behavior when encountering a column parse failure during CSV parsing.
    /// </summary>
    public enum ParseFailure
    {
        ThrowException,
        SkipColumn,
        SetValueTo,
        UseDefaultParseFailure
    }
    public enum StringTrim
    {
        TrimUntilStringDelimiter,
        TrimBeforeAndAfterStringDelimiter,
    }

    public interface ICsvMapBuilder<T>
    {
        ICsvMapBuilder<T> RowDelimiter(string rowDelimiter);

        ICsvMapBuilder<T> HeaderDelimiter(string headerDelimiter);

        ICsvMapBuilder<T> ColumnDelimiter(string columnDelimiter);

        ICsvMapBuilder<T> ColumnTrimChars(char[] trimChars);

        ICsvMapBuilder<T> DefaultParseFailureBehavior(ParseFailureDefault columnParseFailureDefault = ParseFailureDefault.ThrowException);

        ICsvMapBuilder<T> AllowTooFewColumns();

        ICsvMapBuilder<T> StringDelimiter(char stringDelimiter = '"', char stringDelimiterEscape = '\\');
        ICsvMapBuilder<T> StringTrimOptions(bool trimStringDelimiter = true, StringTrim stringOptions = StringTrim.TrimUntilStringDelimiter);

        ICsvMapBuilder<T> ChooseEmptyConstructor();
        ICsvMapBuilder<T> ChooseConstructor<P1>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12, P13>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12, P13, P14>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12, P13, P14, P15>();
        ICsvMapBuilder<T> ChooseConstructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12, P13, P14, P15, P16>();

         IMapToColumnBuilder<T> MapToColumns();

        IMapToColumnBuilder<T> AutoMapByIndex();

        IMapToColumnBuilder<T> AutoMapByName(StringComparison nameMappingStringComparision = StringComparison.Ordinal, char[] headerTrimChars = null, char[] headerRemoveChars = null);
    }

    public interface IMapToColumnBuilder<T>
    {
        IMapToColumnOptionsBuilder<T, P> MapToColumn<P>(Expression<Func<T, P>> property, int csvColumnIndex);

        IMapToColumnOptionsBuilder<T, P> MapToColumn<P>(Expression<Func<T, P>> property, string csvColumnName);

        IMapToColumnBuilder<T> IgnoreProperty<P>(Expression<Func<T, P>> property); //Can only be used in an Automap situation
    }

    public interface IMapToColumnOptionsBuilder<T, P> : IMapToColumnBuilder<T>
    {
        IMapToColumnOptionsBuilder<T, P> OnParseFailure(ParseFailure parseFailure, P valueToSetOnParseFail = default);
        IMapToColumnOptionsBuilder<T, P> CustomParse(ReadOnlySpanFunc<char, long, int, P> customParse = null);
    }

}
