using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Coffe_Grinder
{
    public partial class CreateOrderPage : Page
    {

        private readonly Coffe_Grinder_DB_Entities db = new Coffe_Grinder_DB_Entities();
        private ObservableCollection<CoffeeItem> coffeeItems;
        private ObservableCollection<OrderItem> orderItems;
        private List<CoffeeType> coffeeTypes;
        private decimal orderTotal;

        public CreateOrderPage()
        {
            InitializeComponent();
            coffeeItems = new ObservableCollection<CoffeeItem>();
            orderItems = new ObservableCollection<OrderItem>();
            OrderItemsGrid.ItemsSource = orderItems;
            Loaded += CreateOrderPage_Loaded;

            AmountPaid.PreviewTextInput += AmountPaid_PreviewTextInput;
            AmountPaid.PreviewKeyDown += AmountPaid_PreviewKeyDown;
            AmountPaid.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(AmountPaid_Pasting));
        }
        private async void CreateOrderPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Check if this is a reload (not the first load)
            if (coffeeItems.Count > 0)
            {
                await RefreshInventoryData();
            }
            else
            {
                await LoadDataAsync();
            }
        }

        private async Task RefreshInventoryData()
        {
            try
            {
                // Force a refresh of all coffee inventory data
                db.ChangeTracker.Entries<CoffeeInventory>().ToList().ForEach(e => e.Reload());

                // Update the MaxQuantity property in the coffeeItems collection
                foreach (var coffeeItem in coffeeItems)
                {
                    var coffee = await db.CoffeeInventories.FindAsync(coffeeItem.CoffeeID);
                    if (coffee != null)
                    {
                        coffeeItem.MaxQuantity = coffee.QuantityInStock;
                        Trace.WriteLine($"Updated CoffeeItem: ID={coffeeItem.CoffeeID}, Name={coffeeItem.CoffeeName}, MaxQuantity={coffeeItem.MaxQuantity:F3}");
                    }
                }

                // Notify the UI of the changes
                var collectionView = CollectionViewSource.GetDefaultView(coffeeItems);
                collectionView.Refresh();

                CoffeeSelectionGrid.ItemsSource = null;
                CoffeeSelectionGrid.ItemsSource = collectionView;

                Trace.WriteLine("Coffee inventory quantities refreshed successfully");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"RefreshInventoryData error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"خطأ في تحديث بيانات المخزون: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Trace.WriteLine("Starting LoadDataAsync...");

                // Load coffee types
                try
                {
                    coffeeTypes = await db.CoffeeTypes.OrderBy(t => t.TypeName).ToListAsync();
                    Trace.WriteLine($"Loaded {coffeeTypes.Count} coffee types.");

                    if (!coffeeTypes.Any())
                    {
                        MessageBox.Show("لم يتم العثور على أنواع القهوة في قاعدة البيانات.", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                        coffeeTypes = new List<CoffeeType>();
                    }

                    var allTypesOption = new CoffeeType { CoffeeTypeID = 0, TypeName = "جميع الأنواع" };
                    var typesWithAll = new List<CoffeeType> { allTypesOption };
                    typesWithAll.AddRange(coffeeTypes);

                    CoffeeTypesList.ItemsSource = typesWithAll;
                    CoffeeTypesList.DisplayMemberPath = "TypeName";
                    CoffeeTypesList.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error loading CoffeeTypes: {ex.Message}");
                    MessageBox.Show($"خطأ في تحميل أنواع القهوة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Load coffee inventory
                try
                {
                    var coffees = await db.CoffeeInventories
                        .Include(c => c.CoffeeType)
                        .OrderBy(c => c.CoffeeName)
                        .ToListAsync();

                    Trace.WriteLine($"Loaded {coffees.Count} coffee inventory items.");

                    if (!coffees.Any())
                    {
                        MessageBox.Show("لم يتم العثور على مخزون القهوة في قاعدة البيانات.", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    coffeeItems.Clear();
                    foreach (var coffee in coffees)
                    {
                        coffeeItems.Add(new CoffeeItem
                        {
                            CoffeeID = coffee.CoffeeID,
                            CoffeeName = coffee.CoffeeName,
                            CoffeeType = coffee.CoffeeType,
                            UnitPrice = coffee.PricePerKg,
                            MaxQuantity = coffee.QuantityInStock,
                            Quantity = 0m,
                            Description = coffee.Description
                        });
                    }

                    // Set ItemsSource to coffeeItems and apply initial filter
                    CoffeeSelectionGrid.ItemsSource = coffeeItems;
                    FilterCoffeeItems();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error loading CoffeeInventories: {ex.Message}");
                    MessageBox.Show($"خطأ في تحميل مخزون القهوة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Unexpected error in LoadDataAsync: {ex.Message}");
                MessageBox.Show($"خطأ غير متوقع في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterCoffeeItems();
        }

        private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow special characters in search box
            // No restrictions needed as per requirements
        }

        private void CoffeeTypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                FilterCoffeeItems();
            }
        }

        private void FilterCoffeeItems()
        {
            try
            {
                if (CoffeeSelectionGrid == null || coffeeItems == null) return;

                // First, refresh the data binding
                var collectionView = CollectionViewSource.GetDefaultView(coffeeItems);

                // Your existing filter logic
                var searchText = SearchBox.Text?.Trim().ToLower() ?? "";
                var selectedType = CoffeeTypesList.SelectedItem as CoffeeType;
                var selectedTypeName = selectedType?.TypeName ?? "جميع الأنواع";

                collectionView.Filter = item =>
                {
                    var coffee = item as CoffeeItem;
                    if (coffee == null) return false;

                    bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                       coffee.CoffeeName?.ToLower().Contains(searchText) == true ||
                                       coffee.Description?.ToLower().Contains(searchText) == true ||
                                       coffee.CoffeeID.ToString().Contains(searchText);

                    bool matchesType = selectedTypeName == "جميع الأنواع" ||
                                      (coffee.CoffeeType != null && coffee.CoffeeType.TypeName == selectedTypeName);

                    return matchesSearch && matchesType;
                };

                // Force a refresh
                collectionView.Refresh();

                CoffeeSelectionGrid.ItemsSource = collectionView;
                Trace.WriteLine($"Set CoffeeSelectionGrid.ItemsSource to CollectionView with {collectionView.Cast<object>().Count()} items");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Filtering error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"خطأ في التصفية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var coffeeId = (int)button.Tag;
            var coffeeItem = coffeeItems.FirstOrDefault(c => c.CoffeeID == coffeeId);

            if (coffeeItem != null)
            {
                decimal newQuantity = coffeeItem.Quantity + 1m;
                if (newQuantity <= coffeeItem.MaxQuantity)
                {
                    coffeeItem.Quantity = Math.Round(newQuantity, 3);
                    UpdateOrderItem(coffeeItem);
                }
                else
                {
                    MessageBox.Show($"الكمية المطلوبة تتجاوز المخزون المتاح: {coffeeItem.MaxQuantity:F3} كجم",
                        "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var coffeeId = (int)button.Tag;
            var coffeeItem = coffeeItems.FirstOrDefault(c => c.CoffeeID == coffeeId);

            if (coffeeItem != null)
            {
                decimal newQuantity = Math.Round(coffeeItem.Quantity - 1m, 3);
                if (newQuantity >= 0)
                {
                    coffeeItem.Quantity = newQuantity;
                    UpdateOrderItem(coffeeItem);
                }
                else
                {
                    coffeeItem.Quantity = 0m;
                    UpdateOrderItem(coffeeItem);
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T && (child as FrameworkElement)?.Name == childName)
                    return (T)child;

                var result = FindVisualChild<T>(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void QuantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var coffeeItem = textBox.DataContext as CoffeeItem;
            if (coffeeItem == null) return;

            var input = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                coffeeItem.Quantity = 0m;
                UpdateOrderItem(coffeeItem);
                return;
            }

            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantityInKg))
            {
                if (quantityInKg >= 0)
                {
                    coffeeItem.Quantity = Math.Round(quantityInKg, 3);
                    UpdateOrderItem(coffeeItem);
                }
                else
                {
                    textBox.Text = coffeeItem.Quantity.ToString("F3");
                    MessageBox.Show("الكمية يجب أن تكون أكبر من أو تساوي صفر.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void QuantityTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var coffeeItem = textBox.DataContext as CoffeeItem;
            if (coffeeItem != null && coffeeItem.Quantity == 0m)
            {
                textBox.Text = "";
            }
            else if (coffeeItem != null)
            {
                textBox.SelectAll();
            }
        }

        private void QuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var coffeeItem = textBox.DataContext as CoffeeItem;
            if (coffeeItem == null) return;

            var input = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                textBox.Text = "0.000";
                coffeeItem.Quantity = 0m;
                UpdateOrderItem(coffeeItem);
                return;
            }

            if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                textBox.Text = coffeeItem.Quantity.ToString("F3");
            }
            else
            {
                UpdateOrderItem(coffeeItem);
            }
        }

        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var text = textBox.Text;
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;

            if (selectionLength > 0)
            {
                text = text.Remove(selectionStart, selectionLength);
            }

            var newText = text.Insert(selectionStart, e.Text);

            if (e.Text == "." && text.Contains("."))
            {
                e.Handled = true;
                return;
            }

            if (!decimal.TryParse(newText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
            {
                e.Handled = true;
                return;
            }
        }

        private void CustomerName_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only Arabic or English letters and spaces
            if (!Regex.IsMatch(e.Text, @"^[\p{L}\s]+$"))
            {
                e.Handled = true;
                MessageBox.Show("اسم العميل يجب أن يحتوي على حروف عربية أو إنجليزية فقط.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CustomerPhone_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only digits
            if (!Regex.IsMatch(e.Text, @"^\d+$"))
            {
                e.Handled = true;
                MessageBox.Show("رقم الهاتف يجب أن يحتوي على أرقام فقط.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AmountPaid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var text = textBox.Text;
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;

            // If text is selected, it will be replaced by the new input
            if (selectionLength > 0)
            {
                text = text.Remove(selectionStart, selectionLength);
            }

            var newText = text.Insert(selectionStart, e.Text);

            // Only allow one decimal point
            if (e.Text == "." && text.Contains("."))
            {
                e.Handled = true;
                return;
            }

            // Check if the new text is a valid decimal number
            if (!decimal.TryParse(newText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
            {
                e.Handled = true;
                // Only show message if the input is not a number at all (avoid message for decimal points)
                if (!char.IsDigit(e.Text[0]) && e.Text != ".")
                {
                    MessageBox.Show("المبلغ المدفوع يجب أن يكون رقمًا صحيحًا أو عشريًا.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }
        }

        private void AmountPaid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Allow control keys like Backspace, Delete, Arrow keys, etc.
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void AmountPaid_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string));
                var textBox = sender as TextBox;
                if (textBox == null) return;

                var currentText = textBox.Text;
                var selectionStart = textBox.SelectionStart;
                var selectionLength = textBox.SelectionLength;

                // If text is selected, it will be replaced by the pasted text
                if (selectionLength > 0)
                {
                    currentText = currentText.Remove(selectionStart, selectionLength);
                }

                var newText = currentText.Insert(selectionStart, text);

                // Check if the new text is a valid decimal number
                if (!decimal.TryParse(newText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                {
                    e.CancelCommand();
                    MessageBox.Show("المبلغ المدفوع يجب أن يكون رقمًا صحيحًا أو عشريًا.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void UpdateOrderItem(CoffeeItem coffeeItem)
        {
            // Remove any existing item for this coffee
            var existingItem = orderItems.FirstOrDefault(o => o.CoffeeID == coffeeItem.CoffeeID);
            if (existingItem != null)
            {
                orderItems.Remove(existingItem);
            }

            // Add new item if quantity > 0
            if (coffeeItem.Quantity > 0)
            {
                orderItems.Add(new OrderItem
                {
                    CoffeeID = coffeeItem.CoffeeID,
                    CoffeeName = coffeeItem.CoffeeName,
                    CoffeeType = coffeeItem.CoffeeType,
                    Quantity = Math.Round(coffeeItem.Quantity, 3),
                    UnitPrice = Math.Round(coffeeItem.UnitPrice, 2),
                    Subtotal = Math.Round(coffeeItem.Quantity * coffeeItem.UnitPrice, 2)
                });
            }

            UpdateOrderTotal();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var orderItem = button.DataContext as OrderItem;
            if (orderItem != null)
            {
                orderItems.Remove(orderItem);
                var coffeeItem = coffeeItems.FirstOrDefault(c => c.CoffeeID == orderItem.CoffeeID);
                if (coffeeItem != null)
                {
                    coffeeItem.Quantity = 0m;
                }
                UpdateOrderTotal();
                OrderItemsGrid.Items.Refresh();
            }
        }

        private void UpdateOrderTotal()
        {
            orderTotal = orderItems.Sum(o => o.Subtotal);
            OrderTotal.Text = $"{orderTotal:F2} جنيه";
            UpdateChangeAmount();
        }

        private void UpdateChangeAmount()
        {
            if (decimal.TryParse(AmountPaid.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountPaid))
            {
                var change = amountPaid - orderTotal;
                ChangeAmount.Text = $"{change:F2} جنيه";
                ChangeAmount.Foreground = change >= 0 ? Brushes.Green : Brushes.Red;
            }
            else
            {
                ChangeAmount.Text = "0.00 جنيه";
                ChangeAmount.Foreground = Brushes.Green;
            }
        }

        private void AmountPaid_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateChangeAmount();
        }

        private async Task<Order> SubmitOrderToDatabase()
        {
            try
            {
                // Validate Customer Name
                if (string.IsNullOrWhiteSpace(CustomerName.Text))
                {
                    MessageBox.Show("يرجى إدخال اسم العميل.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                if (!Regex.IsMatch(CustomerName.Text, @"^[\p{L}\s]+$"))
                {
                    MessageBox.Show("اسم العميل يجب أن يحتوي على حروف عربية أو إنجليزية فقط.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                if (CustomerName.Text.Length < 2 || CustomerName.Text.Length > 50)
                {
                    MessageBox.Show("اسم العميل يجب أن يكون بين 2 و 50 حرفًا.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                // Validate Phone Number
                if (!string.IsNullOrWhiteSpace(CustomerPhone.Text))
                {
                    if (!Regex.IsMatch(CustomerPhone.Text, @"^\d{10,15}$"))
                    {
                        MessageBox.Show("رقم الهاتف يجب أن يحتوي على أرقام فقط ويكون بين 10 و 15 رقمًا.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }
                }

                if (!orderItems.Any())
                {
                    MessageBox.Show("يرجى إضافة عنصر واحد على الأقل إلى الطلب.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                if (!decimal.TryParse(AmountPaid.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountPaid) || amountPaid < orderTotal)
                {
                    MessageBox.Show($"المبلغ المدفوع ({AmountPaid.Text}) أقل من الإجمالي ({orderTotal:F2}).", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    StatusID = 1,
                    CustomerName = CustomerName.Text,
                    TotalPrice = orderTotal,
                    UserID = 1,
                    PhoneNumber = CustomerPhone.Text,
                    Notes = OrderNotes.Text
                };

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                foreach (var item in orderItems)
                {
                    db.OrderDetails.Add(new OrderDetail
                    {
                        OrderID = order.OrderID,
                        CoffeeID = item.CoffeeID,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });

                    var coffee = await db.CoffeeInventories.FindAsync(item.CoffeeID);
                    coffee.QuantityInStock -= item.Quantity;
                    coffee.LastUpdated = DateTime.Now;

                    // Update the MaxQuantity in the coffeeItems collection
                    var coffeeItem = coffeeItems.FirstOrDefault(c => c.CoffeeID == item.CoffeeID);
                    if (coffeeItem != null)
                    {
                        coffeeItem.MaxQuantity = coffee.QuantityInStock;
                        Trace.WriteLine($"Updated CoffeeItem: CoffeeID={coffeeItem.CoffeeID}, New MaxQuantity={coffeeItem.MaxQuantity:F3}");
                    }
                }

                await db.SaveChangesAsync();

                // Reapply the filter to refresh the UI
                FilterCoffeeItems();

                return order;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error submitting order: {ex.Message}");
                MessageBox.Show($"خطأ أثناء إرسال الطلب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async void SubmitOrder_Click(object sender, RoutedEventArgs e)
        {
            var order = await SubmitOrderToDatabase();
            if (order != null)
            {
                MessageBox.Show("تم إنشاء الطلب بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearOrder_Click(null, null);
            }
        }

        private void ClearOrder_Click(object sender, RoutedEventArgs e)
        {
            CustomerName.Text = "";
            CustomerPhone.Text = "";
            OrderNotes.Text = "";
            AmountPaid.Text = "";
            orderItems.Clear();
            foreach (var coffeeItem in coffeeItems)
            {
                coffeeItem.Quantity = 0m;
            }
            UpdateOrderTotal();
            OrderItemsGrid.Items.Refresh();
        }

        private async void PrintOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(CustomerName.Text))
                {
                    MessageBox.Show("يرجى إدخال اسم العميل.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Regex.IsMatch(CustomerName.Text, @"^[\p{L}\s]+$"))
                {
                    MessageBox.Show("اسم العميل يجب أن يحتوي على حروف عربية أو إنجليزية فقط.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (CustomerName.Text.Length < 2 || CustomerName.Text.Length > 50)
                {
                    MessageBox.Show("اسم العميل يجب أن يكون بين 2 و 50 حرفًا.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(CustomerPhone.Text))
                {
                    if (!Regex.IsMatch(CustomerPhone.Text, @"^\d{10,15}$"))
                    {
                        MessageBox.Show("رقم الهاتف يجب أن يحتوي على أرقام فقط ويكون بين 10 و 15 رقمًا.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (!orderItems.Any())
                {
                    MessageBox.Show("يرجى إضافة عنصر واحد على الأقل إلى الطلب.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(AmountPaid.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountPaid) || amountPaid < orderTotal)
                {
                    MessageBox.Show($"المبلغ المدفوع ({AmountPaid.Text}) أقل من الإجمالي ({orderTotal:F2}).", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Find the StatusID for "مكتمل"
                var completedStatus = db.OrderStatuses.FirstOrDefault(s => s.StatusName == "مكتمل");
                if (completedStatus == null)
                {
                    Trace.WriteLine("Error: Could not find 'مكتمل' status in database");
                    MessageBox.Show("خطأ: لا يمكن العثور على حالة 'مكتمل' في قاعدة البيانات.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Save order to database
                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    StatusID = completedStatus.StatusID,
                    CustomerName = CustomerName.Text,
                    TotalPrice = orderTotal,
                    UserID = 1,
                    PhoneNumber = CustomerPhone.Text,
                    Notes = OrderNotes.Text
                };

                db.Orders.Add(order);
                await db.SaveChangesAsync();
                Trace.WriteLine($"Saved Order: OrderID={order.OrderID}, StatusID={order.StatusID}, CustomerName={order.CustomerName}, TotalPrice={order.TotalPrice:F2}");

                // Save order details
                foreach (var item in orderItems)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderID = order.OrderID,
                        CoffeeID = item.CoffeeID,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    };
                    db.OrderDetails.Add(orderDetail);
                    Trace.WriteLine($"Added OrderDetail: OrderID={order.OrderID}, CoffeeID={item.CoffeeID}, Quantity={item.Quantity:F3}, UnitPrice={item.UnitPrice:F2}");

                    var coffee = await db.CoffeeInventories.FindAsync(item.CoffeeID);
                    if (coffee != null)
                    {
                        coffee.QuantityInStock -= item.Quantity;
                        coffee.LastUpdated = DateTime.Now;
                        Trace.WriteLine($"Updated CoffeeID={coffee.CoffeeID}, New QuantityInStock={coffee.QuantityInStock:F3}");

                        // Update the MaxQuantity in the coffeeItems collection
                        var coffeeItem = coffeeItems.FirstOrDefault(c => c.CoffeeID == item.CoffeeID);
                        if (coffeeItem != null)
                        {
                            coffeeItem.MaxQuantity = coffee.QuantityInStock;
                            Trace.WriteLine($"Updated CoffeeItem: CoffeeID={coffeeItem.CoffeeID}, New MaxQuantity={coffeeItem.MaxQuantity:F3}");
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"Error: CoffeeID={item.CoffeeID} not found in CoffeeInventories");
                    }
                }

                await db.SaveChangesAsync();
                Trace.WriteLine("Saved OrderDetails and updated CoffeeInventories");

                // Reapply the filter to refresh the UI
                FilterCoffeeItems();

                // Prepare data for printing
                Trace.WriteLine($"Preparing orderData: OrderItemsGrid.Items.Count={OrderItemsGrid.Items.Count}");
                var orderData = new
                {
                    CustomerName = CustomerName.Text,
                    CustomerPhone = CustomerPhone.Text,
                    OrderStatus = "مكتمل",
                    OrderNotes = OrderNotes.Text,
                    OrderTotal = $"{OrderTotal.Text}",
                    AmountPaid = $"{AmountPaid.Text} جنيه",
                    ChangeAmount = $"{ChangeAmount.Text}",
                    OrderItems = OrderItemsGrid.Items.Cast<OrderItem>().Select(item => new
                    {
                        CoffeeName = item.CoffeeName ?? "غير محدد",
                        CoffeeType = item.CoffeeType?.TypeName ?? "غير محدد",
                        Quantity = item.Quantity.ToString("F3", CultureInfo.InvariantCulture),
                        UnitPrice = $"{item.UnitPrice:F2} جنيه",
                        Subtotal = $"{item.Subtotal:F2} جنيه"
                    }).ToList()
                };

                // Debug orderData
                Trace.WriteLine($"orderData: CustomerName={orderData.CustomerName}, Phone={orderData.CustomerPhone}, Total={orderData.OrderTotal}, Status={orderData.OrderStatus}");
                Trace.WriteLine($"orderData.OrderItems.Count: {orderData.OrderItems.Count}");
                foreach (var item in orderData.OrderItems)
                {
                    Trace.WriteLine($"OrderItem: Name={item.CoffeeName}, Type={item.CoffeeType}, Qty={item.Quantity}, Price={item.UnitPrice}, Subtotal={item.Subtotal}");
                }

                // Show print preview
                var invoiceWindow = new InvoiceWindow(orderData);
                invoiceWindow.Show();

                MessageBox.Show("تم إنشاء الطلب وفتح نافذة الطباعة بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear the order form
                ClearOrder_Click(null, null);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error in PrintOrder: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"خطأ أثناء إنشاء أو طباعة الطلب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class CoffeeItem : INotifyPropertyChanged
        {
            private decimal _quantity;
            private decimal _maxQuantity;

            public int CoffeeID { get; set; }
            public string CoffeeName { get; set; }
            public CoffeeType CoffeeType { get; set; }
            public decimal UnitPrice { get; set; }

            public decimal MaxQuantity
            {
                get => _maxQuantity;
                set
                {
                    _maxQuantity = value;
                    OnPropertyChanged();
                }
            }

            public decimal Quantity
            {
                get => _quantity;
                set
                {
                    _quantity = value;
                    OnPropertyChanged();
                }
            }

            public string Description { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                Trace.WriteLine($"PropertyChanged: {propertyName}");
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private class OrderItem : INotifyPropertyChanged
        {
            private decimal _quantity;
            private decimal _subtotal;

            public int CoffeeID { get; set; }
            public string CoffeeName { get; set; }
            public CoffeeType CoffeeType { get; set; }
            public decimal UnitPrice { get; set; }

            public decimal Quantity
            {
                get => _quantity;
                set
                {
                    _quantity = value;
                    OnPropertyChanged();
                }
            }

            public decimal Subtotal
            {
                get => _subtotal;
                set
                {
                    _subtotal = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void OrderItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.DisplayIndex == 1) // Quantity column
            {
                try
                {
                    var orderItem = e.Row.Item as OrderItem;
                    if (orderItem == null) return;

                    var textBox = e.EditingElement as TextBox;
                    if (textBox == null) return;

                    // Parse the new quantity
                    if (decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newQuantity))
                    {
                        newQuantity = Math.Round(newQuantity, 3);

                        // Find the corresponding coffee item
                        var coffeeItem = coffeeItems.FirstOrDefault(c => c.CoffeeID == orderItem.CoffeeID);
                        if (coffeeItem == null) return;

                        // Validate against max quantity
                        if (newQuantity > coffeeItem.MaxQuantity)
                        {
                            MessageBox.Show($"الكمية المطلوبة تتجاوز المخزون المتاح: {coffeeItem.MaxQuantity:F3} كجم",
                                "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);

                            // Revert to the old value
                            textBox.Text = orderItem.Quantity.ToString("F3");
                            e.Cancel = true;
                            return;
                        }

                        if (newQuantity < 0)
                        {
                            MessageBox.Show("الكمية يجب أن تكون أكبر من أو تساوي صفر.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                            textBox.Text = orderItem.Quantity.ToString("F3");
                            e.Cancel = true;
                            return;
                        }

                        // Update the coffee item quantity
                        coffeeItem.Quantity = newQuantity;

                        // Update the order item
                        orderItem.Quantity = newQuantity;
                        orderItem.Subtotal = Math.Round(newQuantity * orderItem.UnitPrice, 2);

                        // If quantity is set to 0, remove the item
                        if (newQuantity <= 0)
                        {
                            orderItems.Remove(orderItem);
                            coffeeItem.Quantity = 0m;
                            OrderItemsGrid.Items.Refresh();
                        }

                        // Update the order total
                        UpdateOrderTotal();
                    }
                    else
                    {
                        // Invalid input, revert
                        textBox.Text = orderItem.Quantity.ToString("F3");
                        e.Cancel = true;
                    }
                }
                catch (Exception ex)    
                {
                    MessageBox.Show($"خطأ أثناء تحديث الكمية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                }
            }
        }

        private void OrderItemsGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Column.DisplayIndex == 1) // Quantity column
            {
                var textBox = e.EditingElement as TextBox;
                if (textBox != null)
                {
                    textBox.SelectAll();

                    // Add input validation for decimal numbers only
                    textBox.PreviewTextInput += (s, args) =>
                    {
                        var tb = s as TextBox;
                        if (tb == null) return;

                        var text = tb.Text;
                        var selectionStart = tb.SelectionStart;
                        var selectionLength = tb.SelectionLength;

                        if (selectionLength > 0)
                        {
                            text = text.Remove(selectionStart, selectionLength);
                        }

                        var newText = text.Insert(selectionStart, args.Text);

                        if (args.Text == "." && text.Contains("."))
                        {
                            args.Handled = true;
                            return;
                        }

                        if (!decimal.TryParse(newText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                        {
                            args.Handled = true;
                            return;
                        }
                    };
                }
            }
        }

        private void OrderNotes_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (textBox.Text.Length > 500)
            {
                textBox.Text = textBox.Text.Substring(0, 500);
                textBox.Select(textBox.Text.Length, 0);
                MessageBox.Show("الملاحظات لا يمكن أن تتجاوز 500 حرف.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OrderItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Implementation if needed
        }

        private void NavigateToOrders_Click(object sender, RoutedEventArgs e)
        {
            OrdersPage ordersPage = new OrdersPage();
            this.NavigationService.Navigate(ordersPage);
        }


    }
}