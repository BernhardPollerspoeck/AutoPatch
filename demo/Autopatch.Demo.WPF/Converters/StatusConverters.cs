using Autopatch.Demo.Shared;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Autopatch.Demo.WPF.Converters;

/// <summary>
/// Converts OrderStatus enum to corresponding color for UI display.
/// </summary>
public class OrderStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Received => Colors.LightBlue,
                OrderStatus.Preparing => Colors.Yellow,
                OrderStatus.Baking => Colors.Orange,
                OrderStatus.Ready => Colors.LimeGreen,
                OrderStatus.OutForDelivery => Colors.Red,
                OrderStatus.Delivered => Colors.Gray,
                _ => Colors.LightGray
            };
        }
        return Colors.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DriverStatus enum to corresponding color for UI display.
/// </summary>
public class DriverStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DriverStatus status)
        {
            return status switch
            {
                DriverStatus.Available => Colors.LimeGreen,
                DriverStatus.Assigned => Colors.Orange,
                DriverStatus.Delivering => Colors.Red,
                DriverStatus.Returning => Colors.DodgerBlue,
                DriverStatus.Offline => Colors.Gray,
                _ => Colors.LightGray
            };
        }
        return Colors.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts List of strings to comma-separated display string.
/// </summary>
public class ItemsListConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is List<string> items)
        {
            return string.Join(", ", items);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ETA DateTime to color based on urgency.
/// </summary>
public class ETAToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime eta)
        {
            var minutesUntilDelivery = (eta - DateTime.Now).TotalMinutes;
            
            if (minutesUntilDelivery < 5)
                return Brushes.Red;      // Urgent
            else if (minutesUntilDelivery < 15)
                return Brushes.Orange;   // Soon
            else
                return Brushes.Green;    // On time
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
