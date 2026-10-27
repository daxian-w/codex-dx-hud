using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CodexRateMonitorNative
{
    // Small renderer for the bundled, versioned monochrome SVG asset. It uses
    // the original path data rather than approximating the brand with shapes.
    internal sealed class SvgMark : IDisposable
    {
        private readonly GraphicsPath path;
        public SvgMark(string file)
        {
            string svg = File.ReadAllText(file);
            Match attribute = Regex.Match(svg, "<path[^>]*\\sd=\"([^\"]+)\"");
            if (!attribute.Success) throw new FormatException("Missing SVG path");
            path = Parse(attribute.Groups[1].Value);
        }
        public void Dispose() { path.Dispose(); }
        public void Draw(Graphics graphics, RectangleF bounds, Color color)
        {
            using (var drawing = (GraphicsPath)path.Clone())
            using (var matrix = new Matrix(bounds.Width / 24f, 0, 0, bounds.Height / 24f, bounds.X, bounds.Y))
            using (var brush = new SolidBrush(color))
            {
                drawing.Transform(matrix);
                graphics.FillPath(brush, drawing);
            }
        }
        private static GraphicsPath Parse(string text)
        {
            MatchCollection tokens = Regex.Matches(text, "[a-zA-Z]|[-+]?(?:[0-9]*\\.[0-9]+|[0-9]+\\.?[0-9]*)(?:[eE][-+]?[0-9]+)?");
            var result = new GraphicsPath(FillMode.Winding);
            PointF current = PointF.Empty, start = PointF.Empty;
            char command = ' ';
            int index = 0;
            try
            {
                while (index < tokens.Count)
                {
                    if (char.IsLetter(tokens[index].Value[0])) command = tokens[index++].Value[0];
                    bool relative = char.IsLower(command);
                    char operation = char.ToUpperInvariant(command);
                    if (operation == 'Z') { result.CloseFigure(); current = start; command = ' '; continue; }
                    if (index >= tokens.Count) throw new FormatException("Incomplete SVG path");
                    if (operation == 'M' || operation == 'L')
                    {
                        float x = Number(tokens, ref index), y = Number(tokens, ref index);
                        PointF next = new PointF(x + (relative ? current.X : 0), y + (relative ? current.Y : 0));
                        if (operation == 'M') { result.StartFigure(); start = next; command = relative ? 'l' : 'L'; }
                        else result.AddLine(current, next);
                        current = next;
                    }
                    else if (operation == 'H' || operation == 'V')
                    {
                        float value = Number(tokens, ref index);
                        PointF next = operation == 'H' ? new PointF(value + (relative ? current.X : 0), current.Y)
                            : new PointF(current.X, value + (relative ? current.Y : 0));
                        result.AddLine(current, next); current = next;
                    }
                    else if (operation == 'A')
                    {
                        float rx = Math.Abs(Number(tokens, ref index)), ry = Math.Abs(Number(tokens, ref index));
                        float rotation = Number(tokens, ref index);
                        bool large = Number(tokens, ref index) != 0, sweep = Number(tokens, ref index) != 0;
                        float x = Number(tokens, ref index), y = Number(tokens, ref index);
                        PointF next = new PointF(x + (relative ? current.X : 0), y + (relative ? current.Y : 0));
                        if (rotation != 0) throw new FormatException("Bundled mark only uses unrotated arcs");
                        Arc(result, current, next, rx, ry, large, sweep);
                        current = next;
                    }
                    else throw new FormatException("Unsupported bundled SVG command");
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        private static float Number(MatchCollection tokens, ref int index)
        {
            if (index >= tokens.Count) throw new FormatException("Incomplete SVG number");
            return float.Parse(tokens[index++].Value, CultureInfo.InvariantCulture);
        }
        private static void Arc(GraphicsPath result, PointF from, PointF to, double rx, double ry, bool large, bool sweep)
        {
            if (rx == 0 || ry == 0 || from == to) { result.AddLine(from, to); return; }
            double x = (from.X - to.X) / 2d, y = (from.Y - to.Y) / 2d;
            double lambda = x * x / (rx * rx) + y * y / (ry * ry);
            if (lambda > 1) { double factor = Math.Sqrt(lambda); rx *= factor; ry *= factor; }
            double rx2 = rx * rx, ry2 = ry * ry;
            double denominator = rx2 * y * y + ry2 * x * x;
            double multiplier = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0,
                (rx2 * ry2 - rx2 * y * y - ry2 * x * x) / denominator));
            double cx = multiplier * rx * y / ry + (from.X + to.X) / 2d;
            double cy = multiplier * -ry * x / rx + (from.Y + to.Y) / 2d;
            double first = Math.Atan2((from.Y - cy) / ry, (from.X - cx) / rx);
            double last = Math.Atan2((to.Y - cy) / ry, (to.X - cx) / rx);
            double delta = last - first;
            if (!sweep && delta > 0) delta -= Math.PI * 2;
            if (sweep && delta < 0) delta += Math.PI * 2;
            result.AddArc(new RectangleF((float)(cx - rx), (float)(cy - ry), (float)(rx * 2), (float)(ry * 2)),
                (float)(first * 180 / Math.PI), (float)(delta * 180 / Math.PI));
        }
    }
}
