using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Windows.Data;
using System.Diagnostics;

namespace Coffe_Grinder
{
    public partial class OrdersPage : Page
    {
        private readonly Coffe_Grinder_DB_Entities db = new Coffe_Grinder_DB_Entities();

        public OrdersPage()
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ar-SA");
            InitializeComponent();
            LoadOrders();
            OrdersDataGrid.SelectionChanged += OrdersDataGrid_SelectionChanged;
            DataContext = this;
        }

        private void CancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.DataContext is Order order))
            {
                return;
            }

            const int CancelledStatusId = 2;
            if (order.StatusID == CancelledStatusId)
            {
                MessageBox.Show("الطلب ملغى بالفعل.", "معلومات",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("هل تريد فعلاً إلغاء هذا الطلب؟ سيتم استعادة الكميات إلى المخزون.",
                "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                UpdateOrderStatus(order, CancelledStatusId, order.StatusID);
            }
        }

        private void UpdateOrderStatus(Order order, int newStatusId, int? oldStatusId)
        {
            const int CancelledStatusId = 2;
            DbContextTransaction transaction = null;
            try
            {
                // Validate StatusID exists
                if (!db.OrderStatuses.Any(s => s.StatusID == newStatusId))
                {
                    throw new InvalidOperationException($"StatusID {newStatusId} does not exist in OrderStatuses");
                }

                transaction = db.Database.BeginTransaction();
                Debug.WriteLine($"Updating OrderID {order.OrderID}: Old StatusID={oldStatusId}, New StatusID={newStatusId}");

                // Re-attach the order if detached
                var trackedOrder = db.Orders.Local.FirstOrDefault(o => o.OrderID == order.OrderID);
                if (trackedOrder == null)
                {
                    trackedOrder = db.Orders.Find(order.OrderID);
                    if (trackedOrder == null)
                    {
                        throw new InvalidOperationException($"OrderID {order.OrderID} not found in database");
                    }
                }

                // Update StatusID
                trackedOrder.StatusID = newStatusId;
                db.Entry(trackedOrder).Property(o => o.StatusID).IsModified = true;

                // Save StatusID first
                db.SaveChanges();
                Debug.WriteLine($"Saved OrderID {order.OrderID}: StatusID={trackedOrder.StatusID}");

                // Restore inventory if cancelling
                if (newStatusId == CancelledStatusId)
                {
                    try
                    {
                        RestoreInventoryQuantities(order.OrderID);
                        db.SaveChanges();
                        Debug.WriteLine($"Inventory restored for OrderID {order.OrderID}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Inventory Restore Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                        MessageBox.Show($"تم تحديث حالة الطلب، لكن فشل استعادة المخزون: {ex.Message}", "تحذير",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                transaction.Commit();
                Debug.WriteLine($"Transaction committed for OrderID {order.OrderID}");

                LoadOrders();
                MessageBox.Show(newStatusId == CancelledStatusId
                    ? $"تم إلغاء الطلب بنجاح. "
                    : $"تم تحديث حالة الطلب بنجاح. الحالة: {trackedOrder.StatusID}", "نجاح",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (DbUpdateException dbEx)
            {
                Debug.WriteLine($"DbUpdateException: {dbEx.InnerException?.Message ?? dbEx.Message}, StackTrace: {dbEx.StackTrace}");
                MessageBox.Show($"خطأ في تحديث قاعدة البيانات: {dbEx.InnerException?.Message ?? dbEx.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                transaction?.Rollback();
                LoadOrders();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في تحديث حالة الطلب: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                transaction?.Rollback();
                LoadOrders();
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private void RestoreInventoryQuantities(int orderId)
        {
            var orderDetails = db.OrderDetails
                .Where(od => od.OrderID == orderId)
                .Include(od => od.CoffeeInventory)
                .ToList();

            if (!orderDetails.Any())
            {
                Debug.WriteLine($"No order details found for OrderID {orderId}");
                return;
            }

            foreach (var detail in orderDetails)
            {
                if (detail.CoffeeInventory == null || !detail.Quantity.HasValue)
                {
                    throw new InvalidOperationException($"بيانات تفاصيل الطلب غير صالحة: CoffeeInventory أو Quantity فارغ لـ OrderDetailID {detail.OrderDetailID}");
                }
                detail.CoffeeInventory.QuantityInStock += detail.Quantity.Value;
                db.Entry(detail.CoffeeInventory).State = EntityState.Modified;
                Debug.WriteLine($"Restored {detail.Quantity.Value} kg to CoffeeID {detail.CoffeeInventory.CoffeeID}, New QuantityInStock={detail.CoffeeInventory.QuantityInStock}");
            }
        }

        private void OrdersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OrdersDataGrid.SelectedItem is Order selectedOrder)
            {
                LoadOrderDetails(selectedOrder.OrderID);
                DisplayOrderNotes(selectedOrder);
            }
            else
            {
                OrderDetailsDataGrid.ItemsSource = null;
                OrderNotesTextBox.Text = string.Empty;
            }
        }

        private void LoadOrders()
        {
            try
            {
                db.ChangeTracker.Entries().ToList().ForEach(e => e.Reload());
                var orders = db.Orders
                    .Include(o => o.OrderStatus)
                    .AsNoTracking()
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();
                OrdersDataGrid.ItemsSource = null;
                OrdersDataGrid.ItemsSource = orders;
                OrdersDataGrid.SelectedItem = null;
                OrderDetailsDataGrid.ItemsSource = null;
                OrderNotesTextBox.Text = string.Empty;
                Debug.WriteLine($"Loaded {orders.Count} orders");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadOrders Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في تحميل الطلبات: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOrderDetails(int orderId)
        {
            try
            {
                db.OrderDetails
                    .Where(od => od.OrderID == orderId)
                    .Include(od => od.CoffeeInventory)
                    .Load();

                var orderDetails = db.OrderDetails.Local
                    .Where(od => od.OrderID == orderId)
                    .Select(od => new
                    {
                        od.OrderDetailID,
                        od.CoffeeInventory,
                        od.Quantity,
                        od.UnitPrice,
                        Subtotal = od.Quantity * od.UnitPrice
                    })
                    .ToList();

                OrderDetailsDataGrid.ItemsSource = orderDetails;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadOrderDetails Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في تحميل تفاصيل الطلب: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayOrderNotes(Order order)
        {
            OrderNotesTextBox.Text = string.IsNullOrWhiteSpace(order.Notes)
                ? "لا توجد ملاحظات متاحة"
                : order.Notes;
        }

        private void RefreshOrders(object sender, RoutedEventArgs e)
        {
            try
            {
                db.SaveChanges();
                LoadOrders();
                MessageBox.Show("تم تحديث الطلبات بنجاح.", "نجاح",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshOrders Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في تحديث الطلبات: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }

    
}