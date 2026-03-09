using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MarcoERP.WpfUI.Converters
{
    /// <summary>
    /// Converts bool to PackIcon Kind name: true = "Check", false = "Close".
    /// </summary>
    public sealed class BoolToCheckIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return MaterialDesignThemes.Wpf.PackIconKind.Check;
            return MaterialDesignThemes.Wpf.PackIconKind.Close;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts bool to color: true = Green, false = Red.
    /// </summary>
    public sealed class BoolToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));

        static BoolToColorConverter()
        {
            GreenBrush.Freeze();
            RedBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return GreenBrush;
            return RedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts bool: true = "جديد", false = "تعديل".
    /// </summary>
    public sealed class BoolToNewEditConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return "جديد";
            return "تعديل";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts null to Collapsed, non-null to Visible.
    /// </summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Inverts a boolean value.
    /// </summary>
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }

    /// <summary>
    /// Converts bool to Visibility: true = Collapsed, false = Visible (inverse of standard).
    /// </summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts JournalEntryStatus enum to Arabic display string.
    /// </summary>
    public sealed class StatusToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            var status = value.ToString();
            switch (status)
            {
                case "Draft": return "مسودة";
                case "Posted": return "مُرحل";
                case "Reversed": return "مُعكوس";
                case "Setup": return "إعداد";
                case "Cancelled": return "ملغي";
                case "Active": return "نشط";
                case "Inactive": return "غير نشط";
                case "Discontinued": return "متوقف";
                case "Closed": return "مُغلقة";
                case "Open": return "مفتوحة";
                case "Locked": return "مُقفلة";
                case "Sent": return "مُرسل";
                case "Accepted": return "مقبول";
                case "Rejected": return "مرفوض";
                case "Converted": return "محوّل";
                case "Expired": return "منتهي";
                case "Unpaid": return "غير مدفوع";
                case "PartiallyPaid": return "مدفوع جزئياً";
                case "FullyPaid": return "مدفوع بالكامل";
                case "Confirmed": return "مؤكد";
                case "Pending": return "قيد الانتظار";
                default: return status;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Returns true if a decimal value is negative.
    /// </summary>
    public sealed class IsNegativeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d) return d < 0m;
            if (value is double dbl) return dbl < 0.0;
            if (value is int i) return i < 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts SourceType enum to Arabic display string.
    /// </summary>
    public sealed class SourceTypeToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            var src = value.ToString();
            switch (src)
            {
                case "Manual": return "يدوي";
                case "SalesInvoice": return "فاتورة بيع";
                case "PurchaseInvoice": return "فاتورة شراء";
                case "CashReceipt": return "سند قبض";
                case "CashPayment": return "سند صرف";
                case "Inventory": return "مخزون";
                case "Adjustment": return "تعديل";
                case "Opening": return "افتتاحي";
                case "Closing": return "إقفال";
                case "SalesQuotation": return "عرض سعر بيع";
                case "PurchaseQuotation": return "طلب شراء";
                case "PurchaseReturn": return "مرتجع مشتريات";
                case "SalesReturn": return "مرتجع مبيعات";
                case "CashTransfer": return "تحويل نقدي";
                default: return src;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts InvoiceType enum to Arabic display string.
    /// </summary>
    public sealed class InvoiceTypeToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            var type = value.ToString();
            switch (type)
            {
                case "Cash": return "نقدي";
                case "Credit": return "آجل";
                default: return type;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts PaymentMethod enum to Arabic display string.
    /// </summary>
    public sealed class PaymentMethodToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            var method = value.ToString();
            switch (method)
            {
                case "Cash": return "نقدي";
                case "Card": return "بطاقة";
                case "OnAccount": return "آجل";
                case "Check": return "شيك";
                case "BankTransfer": return "تحويل بنكي";
                case "EWallet": return "محفظة إلكترونية";
                default: return method;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Two-way converter between string "true"/"false" and bool.
    /// Used for settings editor ToggleButton binding.
    /// </summary>
    public sealed class StringBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return s.Equals("true", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? "true" : "false";
        }
    }

    /// <summary>
    /// Converts AccountType enum values to Arabic display strings.
    /// </summary>
    public sealed class AccountTypeToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            var type = value.ToString();
            switch (type)
            {
                case "Asset": return "أصول";
                case "Liability": return "خصوم";
                case "Equity": return "حقوق ملكية";
                case "Revenue": return "إيرادات";
                case "COGS": return "تكلفة مبيعات";
                case "Expense": return "مصروفات";
                case "OtherIncome": return "إيرادات أخرى";
                case "OtherExpense": return "مصروفات أخرى";
                default: return type;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts an integer Level to a left Thickness for indentation in flat grids.
    /// Level 1 = 0px, Level 2 = 20px, Level 3 = 40px, Level 4 = 60px.
    /// </summary>
    public sealed class LevelToIndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                double indent = Math.Max(0, (level - 1)) * 20.0;
                return new Thickness(indent, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
