using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace MarcoERP.WpfUI.Views.Inventory
{
    public partial class ProductImportView : UserControl
    {
        public ProductImportView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Converts List&lt;string&gt; to a comma-separated error string.
    /// </summary>
    public sealed class ErrorListConverter : IValueConverter
    {
        public static readonly ErrorListConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<string> errors && errors.Count > 0)
                return string.Join(" | ", errors);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts bool to ✓ / ✗ symbol for status display.
    /// </summary>
    public sealed class BoolToSymbolConverter : IValueConverter
    {
        public static readonly BoolToSymbolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "✓" : "✗";
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

}
