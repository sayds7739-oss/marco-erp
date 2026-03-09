using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace MarcoERP.WpfUI.Views.Sales
{
    public partial class CustomerImportView : UserControl
    {
        public CustomerImportView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Converts List&lt;string&gt; to a comma-separated error string.
    /// </summary>
    public sealed class CustomerImportErrorListConverter : IValueConverter
    {
        public static readonly CustomerImportErrorListConverter Instance = new();

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
    /// Converts bool to checkmark / cross symbol for status display.
    /// </summary>
    public sealed class CustomerImportBoolToSymbolConverter : IValueConverter
    {
        public static readonly CustomerImportBoolToSymbolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "\u2713" : "\u2717";
            return "\u2014";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
