using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MarcoERP.WpfUI.Controls
{
    /// <summary>
    /// Lightweight bar chart rendered using WPF drawing primitives.
    /// Binds to a collection of items with a value and label property.
    /// </summary>
    public sealed class SimpleBarChart : FrameworkElement
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(SimpleBarChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValuePathProperty =
            DependencyProperty.Register(nameof(ValuePath), typeof(string), typeof(SimpleBarChart),
                new FrameworkPropertyMetadata("Value", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelPathProperty =
            DependencyProperty.Register(nameof(LabelPath), typeof(string), typeof(SimpleBarChart),
                new FrameworkPropertyMetadata("Label", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarBrushProperty =
            DependencyProperty.Register(nameof(BarBrush), typeof(Brush), typeof(SimpleBarChart),
                new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxBarsProperty =
            DependencyProperty.Register(nameof(MaxBars), typeof(int), typeof(SimpleBarChart),
                new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
        public string ValuePath { get => (string)GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
        public string LabelPath { get => (string)GetValue(LabelPathProperty); set => SetValue(LabelPathProperty, value); }
        public Brush BarBrush { get => (Brush)GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
        public int MaxBars { get => (int)GetValue(MaxBarsProperty); set => SetValue(MaxBarsProperty, value); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0 || ItemsSource == null) return;

            var items = ItemsSource.Cast<object>().Take(MaxBars).ToList();
            if (items.Count == 0) return;

            var valueProp = items[0].GetType().GetProperty(ValuePath);
            var labelProp = items[0].GetType().GetProperty(LabelPath);
            if (valueProp == null) return;

            var values = items.Select(i => Convert.ToDecimal(valueProp.GetValue(i) ?? 0m)).ToList();
            var labels = items.Select(i => labelProp?.GetValue(i)?.ToString() ?? "").ToList();

            decimal maxVal = values.Count > 0 ? values.Max() : 1m;
            if (maxVal <= 0) maxVal = 1m;

            const double labelHeight = 18;
            const double valueHeight = 14;
            const double topPad = 4;
            double chartHeight = h - labelHeight - valueHeight - topPad;
            if (chartHeight <= 0) return;

            double barWidth = (w - (items.Count + 1) * 4) / items.Count;
            if (barWidth < 8) barWidth = 8;

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var barBrush = BarBrush;
            var labelBrush = new SolidColorBrush(Color.FromRgb(120, 144, 156));
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 0.5);

            // Draw 3 horizontal grid lines
            for (int i = 1; i <= 3; i++)
            {
                double y = topPad + valueHeight + chartHeight * (1 - i / 3.0);
                dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }

            for (int i = 0; i < items.Count; i++)
            {
                double x = 4 + i * (barWidth + 4);
                double barH = chartHeight * (double)(values[i] / maxVal);
                double barY = topPad + valueHeight + chartHeight - barH;

                // Bar with rounded top
                var barRect = new Rect(x, barY, barWidth, barH);
                var barGeo = new RectangleGeometry(barRect, 3, 3);
                dc.DrawGeometry(barBrush, null, barGeo);

                // Value above bar
                var valText = new FormattedText(
                    FormatNumber(values[i]),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 9, labelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                valText.MaxTextWidth = barWidth;
                valText.TextAlignment = TextAlignment.Center;
                dc.DrawText(valText, new Point(x, barY - valueHeight));

                // Label below bar
                var lblText = new FormattedText(
                    labels[i],
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 9, labelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                lblText.MaxTextWidth = barWidth;
                lblText.TextAlignment = TextAlignment.Center;
                lblText.MaxLineCount = 1;
                dc.DrawText(lblText, new Point(x, h - labelHeight));
            }
        }

        private static string FormatNumber(decimal val)
        {
            if (Math.Abs(val) >= 1_000_000)
                return (val / 1_000_000m).ToString("F1") + "M";
            if (Math.Abs(val) >= 1_000)
                return (val / 1_000m).ToString("F1") + "K";
            return val.ToString("F0");
        }
    }
}
