using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

[ValueConversion(typeof(DeviceType?), typeof(Geometry))]
public sealed class DeviceTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        DeviceIcons.For(value as DeviceType?);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
