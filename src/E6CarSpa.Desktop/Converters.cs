using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace E6CarSpa.Desktop;

/// <summary>Turns a byte[] image (PNG/JPG) into an ImageSource for previews.</summary>
public class BytesToImageConverter : IValueConverter
{
    public object? Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not byte[] bytes || bytes.Length == 0) return null;
        var img = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Inverts a boolean (e.g. enable a button when NOT busy).</summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is bool b && !b;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is bool b && !b;
}

/// <summary>Visible when the string is non-empty, otherwise Collapsed.</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Visible when the value is non-null, otherwise Collapsed.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Visible when the numeric value is greater than zero, otherwise Collapsed.</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var n = value is null ? 0 : System.Convert.ToInt64(value);
        return n > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Visible when true, otherwise Collapsed. Pass "Invert" parameter to flip.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var b = value is bool x && x;
        if ((p as string) == "Invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Returns true when both bound values are equal (reference equality). Used with MultiBinding.</summary>
public class EqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c) =>
        values.Length == 2 && Equals(values[0], values[1]);
    public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
}
