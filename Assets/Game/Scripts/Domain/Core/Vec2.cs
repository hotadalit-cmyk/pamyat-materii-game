namespace PM.Domain.Core {
/// <summary>Плоский вектор для чистой Domain-логики. Без UnityEngine (D-20).</summary>
public readonly struct Vec2 {
    public readonly double X;
    public readonly double Y;
    public Vec2(double x, double y) { X = x; Y = y; }
    public static Vec2 Zero => new Vec2(0, 0);
    public static Vec2 Right => new Vec2(1, 0);
    public static Vec2 Up => new Vec2(0, 1);
    public double SqrLength => X * X + Y * Y;
    public double Length => System.Math.Sqrt(SqrLength);
    public Vec2 Normalized() {
        double len = Length;
        return len < 1e-12 ? Zero : new Vec2(X / len, Y / len);
    }
    public Vec2 Scaled(double s) => new Vec2(X * s, Y * s);
    public Vec2 Rotated(double radians) {
        double c = System.Math.Cos(radians), s = System.Math.Sin(radians);
        return new Vec2(X * c - Y * s, X * s + Y * c);
    }
    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => a.Scaled(s);
    public static Vec2 operator *(double s, Vec2 a) => a.Scaled(s);
    public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);
    public bool Approx(Vec2 other, double eps = 1e-5) =>
        System.Math.Abs(X - other.X) <= eps && System.Math.Abs(Y - other.Y) <= eps;
    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}
}
