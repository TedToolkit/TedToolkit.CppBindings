using TedToolkit.CppBindings.Cgal;

public enum GeneratedIntersectionKind
{
    None = 0,
    Point = 1,
    Segment = 2,
}

public readonly record struct GeneratedPoint(double X, double Y);

public readonly record struct GeneratedSegment(GeneratedPoint Source, GeneratedPoint Target);

public readonly record struct GeneratedTransport(int Tag, double AX, double AY, double BX, double BY);

public readonly struct GeneratedIntersection
{
    private readonly GeneratedPoint point;
    private readonly GeneratedSegment segment;

    private GeneratedIntersection(
        GeneratedIntersectionKind kind,
        GeneratedPoint point,
        GeneratedSegment segment)
    {
        Kind = kind;
        this.point = point;
        this.segment = segment;
    }

    public GeneratedIntersectionKind Kind { get; }

    public static GeneratedIntersection FromNative(GeneratedTransport value)
    {
        return value.Tag switch
        {
            0 => new(GeneratedIntersectionKind.None, default, default),
            1 => new(GeneratedIntersectionKind.Point, new(value.AX, value.AY), default),
            2 => new(
                GeneratedIntersectionKind.Segment,
                default,
                new(new(value.AX, value.AY), new(value.BX, value.BY))),
            _ => throw new CgalUnknownResultException(
                $"Native CGAL intersection returned undeclared alternative tag {value.Tag}."),
        };
    }

    public bool TryGetPoint(out GeneratedPoint value)
    {
        value = point;
        return Kind == GeneratedIntersectionKind.Point;
    }

    public bool TryGetSegment(out GeneratedSegment value)
    {
        value = segment;
        return Kind == GeneratedIntersectionKind.Segment;
    }
}
