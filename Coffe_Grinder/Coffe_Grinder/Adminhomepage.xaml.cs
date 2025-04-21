using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;
using System.Collections.Generic;
using System.Diagnostics;

namespace Coffe_Grinder
{
    public partial class Adminhomepage : Page
    {
        private readonly Coffe_Grinder_DB_Entities db = new Coffe_Grinder_DB_Entities();

        public Adminhomepage()
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ar-SA");
            InitializeComponent();
            Loaded += Adminhomepage_Loaded;
            IsVisibleChanged += Adminhomepage_IsVisibleChanged;
        }

        private void Adminhomepage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();
        }

        private void Adminhomepage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                LoadDashboardData();
            }
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();
            MessageBox.Show("تم تحديث البيانات بنجاح", "تحديث البيانات", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadDashboardData()
        {
            try
            {
                // Show loading indicators
                CoffeeLoadingIndicator.Visibility = Visibility.Visible;
                OrdersLoadingIndicator.Visibility = Visibility.Visible;

                // Clear existing data and change tracker
                CoffeeDataGrid.ItemsSource = null;
                OrdersDataGrid.ItemsSource = null;
                LowStockList.ItemsSource = null;
                db.ChangeTracker.Entries().ToList().ForEach(entry => entry.Reload());

                // Load coffee inventory
                var coffeeInventory = db.CoffeeInventories
                    .Include(c => c.CoffeeType)
                    .AsNoTracking()
                    .OrderBy(c => c.CoffeeName)
                    .ToList();
                CoffeeDataGrid.ItemsSource = coffeeInventory;
                Debug.WriteLine($"Loaded {coffeeInventory.Count} coffee inventory items");

                // Load recent orders (last 5)
                var recentOrders = db.Orders
                    .Include(o => o.OrderStatus)
                    .AsNoTracking()
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToList();
                OrdersDataGrid.ItemsSource = recentOrders;
                Debug.WriteLine($"Loaded {recentOrders.Count} recent orders");
                foreach (var order in recentOrders)
                {
                    Debug.WriteLine($"OrderID {order.OrderID}: StatusID={order.StatusID}, StatusName={order.OrderStatus?.StatusName}");
                }

                // Load low stock alerts (less than 10 kg)
                var lowStock = db.CoffeeInventories
                    .Include(c => c.CoffeeType)
                    .AsNoTracking()
                    .Where(c => c.QuantityInStock < 10)
                    .OrderBy(c => c.QuantityInStock)
                    .ToList();
                LowStockList.ItemsSource = lowStock;
                Debug.WriteLine($"Loaded {lowStock.Count} low stock items");

                // Hide loading indicators
                CoffeeLoadingIndicator.Visibility = Visibility.Collapsed;
                OrdersLoadingIndicator.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadDashboardData Exception: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل بيانات لوحة التحكم: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                CoffeeLoadingIndicator.Visibility = Visibility.Collapsed;
                OrdersLoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void ManageInventory_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Uri("inventory.xaml", UriKind.Relative));
        }

        private void ViewAllOrders_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Uri("OrdersPage.xaml", UriKind.Relative));
        }
    }
}