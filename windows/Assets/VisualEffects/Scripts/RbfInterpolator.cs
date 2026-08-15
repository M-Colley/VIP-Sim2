using System;

/// <summary>
/// Radial basis function interpolation over scattered 2-D points, with a linear
/// polynomial tail and Tikhonov regularisation.
///
/// Replaces ALGLIB, which VIP-Sim used for exactly one thing: turning the sparse
/// visual-field grid into a smooth scotoma surface. ALGLIB is dual-licensed and the
/// copy in this repository was the GPL edition, which would have forced the whole
/// application to be GPL on distribution. That is the only reason this exists.
///
/// MathNet.Numerics was the obvious replacement but does not offer scattered 2-D RBF --
/// its interpolation is one-dimensional -- and it is not a first-party dependency of
/// either project, appearing only transitively through another package. Since the
/// system here is at most a few dozen points, a dependency buys nothing that a direct
/// solve does not, and adding one would reintroduce exactly the licence surface this
/// change removes.
///
/// Configuration mirrors what ALGLIB was asked for:
///   rbfsetalgomultilayer(RBase: 5.0, NLayers: 1, LambdaV: 1e-3) with rbfsetlinterm()
/// A single layer reduces to one Gaussian scale, so this is a Gaussian basis at that
/// radius, a linear tail, and that regularisation term. It is NOT bit-identical to
/// ALGLIB's hierarchical solver and is not expected to be; both produce a smooth
/// surface through the same points, which is what the scotoma texture needs.
/// </summary>
public sealed class RbfInterpolator
{
    private readonly double[] _px;      // sample x
    private readonly double[] _py;      // sample y
    private readonly double[] _weights; // one per sample, then 3 polynomial coefficients
    private readonly double _radius;
    private readonly int _n;

    private RbfInterpolator(double[] px, double[] py, double[] weights, double radius)
    {
        _px = px; _py = py; _weights = weights; _radius = radius; _n = px.Length;
    }

    /// <param name="points">N x 3 array of (x, y, value).</param>
    /// <param name="radius">Basis width. Larger is smoother.</param>
    /// <param name="lambda">
    /// Regularisation. Non-zero keeps the system solvable when samples coincide or are
    /// nearly collinear, which a hand-entered clinical grid can easily be, and stops a
    /// near-singular matrix producing wild overshoot between points.
    /// </param>
    /// <returns>Null if there is nothing to interpolate or the solve fails.</returns>
    public static RbfInterpolator Build(double[,] points, double radius = 5.0, double lambda = 1e-3)
    {
        if (points == null) return null;
        int n = points.GetLength(0);
        if (n == 0 || points.GetLength(1) < 3 || radius <= 0.0) return null;

        var px = new double[n];
        var py = new double[n];
        for (int i = 0; i < n; i++) { px[i] = points[i, 0]; py[i] = points[i, 1]; }

        // [ PHI + lambda*I   P ] [w]   [f]
        // [ P^T              0 ] [c] = [0]
        //
        // P is the linear tail [1, x, y]. Its transpose block imposes the orthogonality
        // conditions that keep the polynomial part determined; without them the system is
        // underdetermined and the tail can drift arbitrarily.
        const int poly = 3;
        int m = n + poly;
        var a = new double[m, m];
        var b = new double[m];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) a[i, j] = Basis(px[i], py[i], px[j], py[j], radius);
            a[i, i] += lambda;

            a[i, n] = 1.0; a[i, n + 1] = px[i]; a[i, n + 2] = py[i];
            a[n, i] = 1.0; a[n + 1, i] = px[i]; a[n + 2, i] = py[i];

            b[i] = points[i, 2];
        }

        var w = Solve(a, b);
        return w == null ? null : new RbfInterpolator(px, py, w, radius);
    }

    public double Evaluate(double x, double y)
    {
        double sum = 0.0;
        for (int i = 0; i < _n; i++) sum += _weights[i] * Basis(x, y, _px[i], _py[i], _radius);

        // Linear tail: continues sensibly past the outermost samples instead of decaying
        // to zero, which matters because the grid is sampled well inside the field but the
        // texture is generated across the whole of it.
        return sum + _weights[_n] + _weights[_n + 1] * x + _weights[_n + 2] * y;
    }

    private static double Basis(double ax, double ay, double bx, double by, double radius)
    {
        double dx = ax - bx, dy = ay - by;
        double r2 = (dx * dx + dy * dy) / (radius * radius);
        return Math.Exp(-r2);
    }

    /// <summary>
    /// Gaussian elimination with partial pivoting. The system is (samples + 3) square --
    /// a clinical grid is a few dozen points -- so this is microseconds and there is no
    /// case for anything more elaborate. Partial pivoting is not optional: the saddle-point
    /// block has zeros on its diagonal, so the naive version divides by zero immediately.
    /// </summary>
    private static double[] Solve(double[,] a, double[] b)
    {
        int m = b.Length;

        for (int col = 0; col < m; col++)
        {
            int pivot = col;
            double best = Math.Abs(a[col, col]);
            for (int r = col + 1; r < m; r++)
            {
                double v = Math.Abs(a[r, col]);
                if (v > best) { best = v; pivot = r; }
            }
            if (best < 1e-12) return null; // singular; caller keeps its previous surface

            if (pivot != col)
            {
                for (int c = 0; c < m; c++) { var t = a[col, c]; a[col, c] = a[pivot, c]; a[pivot, c] = t; }
                var tb = b[col]; b[col] = b[pivot]; b[pivot] = tb;
            }

            for (int r = col + 1; r < m; r++)
            {
                double f = a[r, col] / a[col, col];
                if (f == 0.0) continue;
                for (int c = col; c < m; c++) a[r, c] -= f * a[col, c];
                b[r] -= f * b[col];
            }
        }

        var x = new double[m];
        for (int r = m - 1; r >= 0; r--)
        {
            double s = b[r];
            for (int c = r + 1; c < m; c++) s -= a[r, c] * x[c];
            x[r] = s / a[r, r];
        }

        for (int i = 0; i < m; i++) if (double.IsNaN(x[i]) || double.IsInfinity(x[i])) return null;
        return x;
    }
}
