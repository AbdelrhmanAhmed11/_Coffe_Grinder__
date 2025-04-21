using System.Globalization;
using System.Windows.Data;
using System;
using System.Windows;
namespace Coffe_Grinder
{
    public class DecimalToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue == 0m ? "" : decimalValue.ToString("F3", CultureInfo.InvariantCulture);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                {
                    return Math.Round(result, 3);
                }
            }
            return 0m;
        }
    }
}
