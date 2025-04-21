using System;
using System.Globalization;
using System.Windows.Data;

namespace Coffe_Grinder
{
    public class ZeroQuantityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double quantity)
            {
                // Display empty string for zero to allow clearing on focus
                return quantity == 0 ? string.Empty : quantity.ToString("F2", culture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                if (string.IsNullOrWhiteSpace(strValue))
                    return 0.0;
                if (double.TryParse(strValue, NumberStyles.Any, culture, out double result))
                    return result;
            }
            return 0.0;
        }
    }
}
