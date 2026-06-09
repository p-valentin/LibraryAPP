using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BibliotecaApp.Models;

namespace BibliotecaApp.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value != null;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class BookStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value switch
        {
            BookStatus.Available => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
            BookStatus.Borrowed  => new SolidColorBrush(Color.FromRgb(255, 167, 38)),
            _                    => Brushes.Gray
        };
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class LoanStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value switch
        {
            LoanStatus.Active   => new SolidColorBrush(Color.FromRgb(66,  165, 245)),
            LoanStatus.Returned => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
            _                   => Brushes.Gray
        };
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
