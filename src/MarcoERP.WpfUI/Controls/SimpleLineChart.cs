using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MarcoERP.WpfUI.Controls
{
    /// <summary>
    /// Lightweight line chart rendered using WPF drawing primitives.
    /// Draws a polyline with optional filled area underneath.
    /// </summary>
    public sealed class SimpleLineChart : FrameworkElement
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValuePathProperty =
            DependencyProperty.Register(nameof(ValuePath), typeof(string), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata("Value", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelPathProperty =
            DependencyProperty.Register(nameof(LabelPath), typeof(string), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata("Label", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LineBrushProperty =
            DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillBrushProperty =
            DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondValuePathProperty =
            DependencyProperty.Register(nameof(SecondValuePath), typeof(string), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondLineBrushProperty =
            DependencyProperty.Register(nameof(SecondLineBrush), typeof(Brush), typeof(SimpleLineChart),
                new FrameworkPropertyMetadata(Brushes.OrangeRed, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
        public string ValuePath { get => (string)GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
        public string LabelPath { get => (string)GetValue(LabelPathProperty); set => SetValue(LabelPathProperty, value); }
        public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
        public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
        public string SecondValuePath { get => (string)GetValue(SecondValuePathProperty); set => SetValue(SecondValuePathProperty, value); }
        public Brush SecondLineBrush { get => (Brush)GetValue(SecondLineBrushProperty); set => SetValue(SecondLineBrushProperty, value); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0 || ItemsSource == null) return;

            var items = ItemsSource.Cast<object>().ToList();
            if (items.Count < 2) return;

            var type = items[0].GetType();
            var valueProp = type.GetProperty(ValuePath);
            var labelProp = type.GetProperty(LabelPath);
            if (valueProp == null) return;

            var values = items.Select(i => Convert.ToDouble(valueProp.GetValue(i) ?? 0.0)).ToList();
            var labels = items.Select(i => labelProp?.GetValue(i)?.ToString() ?? "").ToList();

            // Optional second line
            var secondProp = !string.IsNullOrEmpty(SecondValuePath) ? type.GetProperty(SecondValuePath) : null;
            var values2 = secondProp != null
                ? items.Select(i => Convert.ToDouble(secondProp.GetValue(i) ?? 0.0)).ToList()
                : null;

            double allMax = values.Max();
            if (values2 != null) allMax = Math.Max(allMax, values2.Max());
            if (allMax <= 0) allMax = 1;

            const double labelHeight = 18;
            const double topPad = 8;
            const double leftPad = 4;
            const double rightPad = 4;
            double chartW = w - leftPad - rightPad;
            double chartH = h - labelHeight - topPad;
            if (chartH <= 0 || chartW <= 0) return;

            double stepX = chartW / (items.Count - 1);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var labelBrush = new SolidColorBrush(Color.FromRgb(120, 144, 156));
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)), 0.5);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Horizontal grid lines
            for (int g = 1; g <= 3; g++)
            {
                double gy = topPad + chartH * (1 - g / 3.0);
                dc.DrawLine(gridPen, new Point(leftPad, gy), new Point(w - rightPad, gy));
            }

            // Draw line and fill
            DrawSeries(dc, values, items.Count, stepX, leftPad, topPad, chartW, chartH, allMax, LineBrush, FillBrush);

            // Second series
            if (values2 != null)
                DrawSeries(dc, values2, items.Count, stepX, leftPad, topPad, chartW, chartH, allMax, SecondLineBrush, null);

            // X-axis labels (every Nth to avoid crowding)
            int labelInterval = Math.Max(1, items.Count / 7);
            for (int i = 0; i < items.Count; i += labelInterval)
            {
                double x = leftPad + i * stepX;
                var lblText = new FormattedText(
                    labels[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 9, labelBrush, dpi);
                lblText.TextAlignment = TextAlignment.Center;
                lblText.MaxTextWidth = stepX * labelInterval;
                lblText.MaxLineCount = 1;
                dc.DrawText(lblText, new Point(x - lblText.MaxTextWidth / 2 + stepX * labelInterval / 2 * 0, h - labelHeight + 2));
            }
        }

        private void DrawSeries(DrawingContext dc, System.Collections.Generic.List<double> values, int count,
            double stepX, double leftPad, double topPad, double chartW, double chartH, double maxVal,
            Brush lineBrush, Brush fillBrush)
        {
            var points = new Point[count];
            for (int i = 0; i < count; i++)
            {
                double x = leftPad + i * stepX;
                double y = topPad + chartH - chartH * (values[i] / maxVal);
                points[i] = new Point(x, y);
            }

            // Fill area under the line
            if (fillBrush != null)
            {
                var fillGeo = new StreamGeometry();
                using (var ctx = fillGeo.Open())
                {
                    ctx.BeginFigure(new Point(points[0].X, topPad + chartH), true, true);
                    ctx.LineTo(points[0], false, false);
                    for (int i = 1; i < count; i++)
                        ctx.LineTo(points[i], false, false);
                    ctx.LineTo(new Point(points[count - 1].X, topPad + chartH), false, false);
                }
                fillGeo.Freeze();
                dc.DrawGeometry(fillBrush, null, fillGeo);
            }

            // Line
            var linePen = new Pen(lineBrush, 2) { LineJoin = PenLineJoin.Round };
            for (int i = 0; i < count - 1; i++)
                dc.DrawLine(linePen, points[i], points[i + 1]);

            // Data points
            var dotBrush = lineBrush;
            for (int i = 0; i < count; i++)
                dc.DrawEllipse(dotBrush, null, points[i], 3, 3);
        }
    }
}
