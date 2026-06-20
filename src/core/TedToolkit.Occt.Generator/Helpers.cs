using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

using Cysharp.Text;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator;

internal sealed class CommentProjection
{
    public static CommentProjection Empty { get; } = new();

    public IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<IDescriptionItem>> ParameterDescriptionItems { get; init; }
        = new ReadOnlyDictionary<string, IReadOnlyList<IDescriptionItem>>(
            new Dictionary<string, IReadOnlyList<IDescriptionItem>>(StringComparer.Ordinal));

    public IReadOnlyList<IDescriptionItem> ReturnTypeDescriptionItems { get; init; } = [];
}

public static class Helpers
{
    public static DataType ToPublicDataType(this ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if ((type.DePointer().DeConst() ?? type.DeConst().DePointer()) is { } constPointer)
        {
            return constPointer.ToPInvokeDataType().RefReadonly;
        }

        if (type.DePointer() is { } pointer)
        {
            return pointer.ToPInvokeDataType().Ref;
        }

        return type.ToPInvokeDataType();
    }

    public static ClangSharp.Type? GetAddingType(this ClangSharp.Type? type)
    {
        if (type is null)
        {
            return null;
        }

        if (DePointer(type) is { } pointer)
        {
            return GetAddingType(pointer);
        }

        if (DeConst(type) is { } constPointer)
        {
            return GetAddingType(constPointer);
        }

        return type;
    }

    private static ClangSharp.Type? DePointer(this ClangSharp.Type? type)
    {
        return type is PointerType or LValueReferenceType or RValueReferenceType ? type.PointeeType : null;
    }

    private static ClangSharp.Type? DeConst(this ClangSharp.Type? type)
    {
        return type?.IsLocalConstQualified is true ? type.Desugar : null;
    }

    public static DataType ToPInvokeDataType(this ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.DeConst() is { } constType)
        {
            return constType.ToPInvokeDataType();
        }

        if (type.DePointer() is { } pointer)
        {
            return pointer.ToPInvokeDataType().Pointer;
        }

        if (type is BuiltinType builtinType)
        {
            return builtinType.ToDataType();
        }

        return new(type.AsString.ToValidCSharpName());
    }

    public static DataType ToDataType(this BuiltinType builtinType)
    {
        ArgumentNullException.ThrowIfNull(builtinType);

        return builtinType.Kind switch
        {
            CXTypeKind.CXType_Void => DataType.Void,
            CXTypeKind.CXType_Bool => DataType.Bool,
            CXTypeKind.CXType_Char_U or CXTypeKind.CXType_UChar => DataType.Byte,
            CXTypeKind.CXType_Char16 or CXTypeKind.CXType_WChar => DataType.Char,
            CXTypeKind.CXType_Char32 => DataType.Uint,
            CXTypeKind.CXType_UShort => DataType.Ushort,
            CXTypeKind.CXType_UInt => DataType.Uint,
            CXTypeKind.CXType_ULong => DataType.FromType<CULong>(),
            CXTypeKind.CXType_ULongLong => DataType.Ulong,
            CXTypeKind.CXType_UInt128 => DataType.FromType<UInt128>(),
            CXTypeKind.CXType_Char_S or CXTypeKind.CXType_SChar => DataType.Sbyte,
            CXTypeKind.CXType_Short => DataType.Short,
            CXTypeKind.CXType_Int => DataType.Int,
            CXTypeKind.CXType_Long => DataType.FromType<CLong>(),
            CXTypeKind.CXType_LongLong => DataType.Long,
            CXTypeKind.CXType_Int128 => DataType.FromType<Int128>(),
            CXTypeKind.CXType_Float => DataType.Float,
            CXTypeKind.CXType_Double => DataType.Double,
            CXTypeKind.CXType_Float16 or CXTypeKind.CXType_Half => DataType.FromType<Half>(),
            CXTypeKind.CXType_NullPtr => DataType.FromType<nint>(),
            CXTypeKind.CXType_LongDouble
                or CXTypeKind.CXType_Overload
                or CXTypeKind.CXType_Dependent
                or CXTypeKind.CXType_ObjCId
                or CXTypeKind.CXType_ObjCClass
                or CXTypeKind.CXType_ObjCSel
                or CXTypeKind.CXType_Float128
                or CXTypeKind.CXType_ShortAccum
                or CXTypeKind.CXType_Accum
                or CXTypeKind.CXType_LongAccum
                or CXTypeKind.CXType_UShortAccum
                or CXTypeKind.CXType_UAccum
                or CXTypeKind.CXType_ULongAccum
                or CXTypeKind.CXType_BFloat16
                or CXTypeKind.CXType_Ibm128
                => throw new NotSupportedException(
                    $"Unsupported builtin type ({builtinType.AsString}, {builtinType.Kind})"),
            _ => throw new NotSupportedException($"Unknown builtin type ({builtinType.AsString}, {builtinType.Kind})"),
        };
    }

    public static string ToValidCSharpName(this string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        using var builder = ZString.CreateStringBuilder();
        var last = false;
        foreach (var c in name)
        {
            if (builder.Length is 0 && char.IsNumber(c))
            {
                AppendUnderscore();
            }
            else if (char.IsLetterOrDigit(c))
            {
                Append(c);
            }
            else
            {
                AppendUnderscore();
            }
        }

        if (last)
        {
            builder.Remove(builder.Length - 1, 1);
        }

        return builder.ToString();

        void Append(char c)
        {
            builder.Append(c);
            last = false;
        }

        void AppendUnderscore()
        {
            if (last)
            {
                return;
            }

            builder.Append('_');
            last = true;
        }
    }

    internal static CommentProjection ToCommentProjection(this Cursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        return cursor.Handle.ToCommentProjection();
    }

    internal static CommentProjection ToCommentProjection(this CXCursor cursor)
    {
        var comment = cursor.ParsedComment;
        if (comment.Kind is CXCommentKind.CXComment_Null || comment.IsWhitespace)
        {
            return CommentProjection.Empty;
        }

        List<IRootDescriptionItem> descriptionItems = [];
        Dictionary<string, IReadOnlyList<IDescriptionItem>> parameterDescriptionItems = new(StringComparer.Ordinal);
        List<IDescriptionItem> returnTypeDescriptionItems = [];
        var hasSummary = false;

        foreach (var child in comment.GetChildren())
        {
            if (child.IsWhitespace)
            {
                continue;
            }

            switch (child.Kind)
            {
                case CXCommentKind.CXComment_Paragraph:
                    {
                        var items = child.ToParagraphDescriptionItems();
                        if (items.Count is 0)
                        {
                            continue;
                        }

                        descriptionItems.Add(hasSummary
                            ? new DescriptionRemarks(items)
                            : new DescriptionSummary(items));
                        hasSummary = true;
                        break;
                    }
                case CXCommentKind.CXComment_BlockCommand:
                    {
                        var commandName = child.BlockCommandComment_CommandName.ToSafeString();
                        var items = child.ToBodyDescriptionItems();
                        if (items.Count is 0)
                        {
                            continue;
                        }

                        switch (commandName)
                        {
                            case "brief":
                            case "short":
                                descriptionItems.Add(new DescriptionSummary(items));
                                hasSummary = true;
                                break;
                            case "remark":
                            case "remarks":
                            case "details":
                            case "note":
                                descriptionItems.Add(new DescriptionRemarks(items));
                                break;
                            case "return":
                            case "returns":
                            case "result":
                                returnTypeDescriptionItems.AddRange(items);
                                break;
                            default:
                                descriptionItems.Add(hasSummary
                                    ? new DescriptionRemarks(items)
                                    : new DescriptionSummary(items));
                                hasSummary = true;
                                break;
                        }

                        break;
                    }
                case CXCommentKind.CXComment_ParamCommand:
                    {
                        var parameterName = child.ParamCommandComment_ParamName.ToSafeString();
                        var items = child.ToBodyDescriptionItems();
                        if (parameterName.Length is not 0 && items.Count is not 0)
                        {
                            parameterDescriptionItems[parameterName] = items;
                        }

                        break;
                    }
                default:
                    {
                        var items = child.ToBodyDescriptionItems();
                        if (items.Count is 0)
                        {
                            continue;
                        }

                        descriptionItems.Add(hasSummary
                            ? new DescriptionRemarks(items)
                            : new DescriptionSummary(items));
                        hasSummary = true;
                        break;
                    }
            }
        }

        return new CommentProjection
        {
            DescriptionItems = descriptionItems,
            ParameterDescriptionItems =
                new ReadOnlyDictionary<string, IReadOnlyList<IDescriptionItem>>(parameterDescriptionItems),
            ReturnTypeDescriptionItems = returnTypeDescriptionItems,
        };
    }

    private static IReadOnlyList<CXComment> GetChildren(this CXComment comment)
    {
        List<CXComment> children = [];
        for (uint i = 0; i < comment.NumChildren; i++)
        {
            children.Add(comment.GetChild(i));
        }

        return children;
    }

    private static IReadOnlyList<IDescriptionItem> ToBodyDescriptionItems(this CXComment comment)
    {
        foreach (var child in comment.GetChildren())
        {
            if (child.Kind is not CXCommentKind.CXComment_Paragraph)
            {
                continue;
            }

            var items = child.ToParagraphDescriptionItems();
            if (items.Count is not 0)
            {
                return items;
            }
        }

        return comment.ToParagraphDescriptionItems();
    }

    private static IReadOnlyList<IDescriptionItem> ToParagraphDescriptionItems(this CXComment comment)
    {
        List<IDescriptionItem> descriptionItems = [];
        foreach (var child in comment.GetChildren())
        {
            var descriptionItem = child.ToDescriptionItem();
            if (descriptionItem is not null)
            {
                descriptionItems.Add(descriptionItem);
            }
        }

        return descriptionItems;
    }

    private static IDescriptionItem? ToDescriptionItem(this CXComment comment)
    {
        if (comment.IsWhitespace)
        {
            return null;
        }

        return comment.Kind switch
        {
            CXCommentKind.CXComment_Text => comment.TextComment_Text.ToDescriptionText(),
            CXCommentKind.CXComment_InlineCommand => comment.ToInlineDescriptionItem(),
            CXCommentKind.CXComment_Paragraph => comment.ToParagraphOrNull(),
            CXCommentKind.CXComment_VerbatimBlockCommand => comment.ToVerbatimBlockDescriptionItem(),
            CXCommentKind.CXComment_VerbatimLine => comment.VerbatimLineComment_Text.ToDescriptionText(),
            CXCommentKind.CXComment_VerbatimBlockLine => comment.VerbatimBlockLineComment_Text.ToDescriptionText(),
            _ => null,
        };
    }

    private static IDescriptionItem? ToInlineDescriptionItem(this CXComment comment)
    {
        List<IDescriptionItem> items = [];
        for (uint i = 0; i < comment.InlineCommandComment_NumArgs; i++)
        {
            var text = comment.InlineCommandComment_GetArgText(i).ToDescriptionText();
            if (text is not null)
            {
                items.Add(text);
            }
        }

        if (items.Count is 0)
        {
            return comment.InlineCommandComment_CommandName.ToDescriptionText();
        }

        return comment.InlineCommandComment_RenderKind switch
        {
            CXCommentInlineCommandRenderKind.CXCommentInlineCommandRenderKind_Bold => new DescriptionBold(items),
            CXCommentInlineCommandRenderKind.CXCommentInlineCommandRenderKind_Monospaced => new DescriptionCode(false,
                items),
            CXCommentInlineCommandRenderKind.CXCommentInlineCommandRenderKind_Emphasized =>
                new DescriptionItalic(items),
            _ => items.Count is 1 ? items[0] : new DescriptionPara(items),
        };
    }

    private static IDescriptionItem? ToParagraphOrNull(this CXComment comment)
    {
        var items = comment.ToParagraphDescriptionItems();
        return items.Count is 0 ? null : new DescriptionPara(items);
    }

    private static IDescriptionItem? ToVerbatimBlockDescriptionItem(this CXComment comment)
    {
        List<IDescriptionItem> items = [];
        foreach (var child in comment.GetChildren())
        {
            var text = child.Kind switch
            {
                CXCommentKind.CXComment_VerbatimBlockLine => child.VerbatimBlockLineComment_Text.ToDescriptionText(),
                CXCommentKind.CXComment_Text => child.TextComment_Text.ToDescriptionText(),
                _ => null,
            };

            if (text is not null)
            {
                items.Add(text);
            }
        }

        return items.Count is 0 ? null : new DescriptionCode(true, items);
    }

    private static DescriptionText? ToDescriptionText(this CXString text)
    {
        var value = text.ToSafeString();
        return value.Length is 0 ? null : new DescriptionText(value);
    }

    private static string ToSafeString(this CXString text)
    {
        return text.ToString().Trim();
    }
}