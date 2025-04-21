using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Globalization;

namespace Coffe_Grinder
{
    public partial class inventory : Page
    {
        private readonly Coffe_Grinder_DB_Entities db = new Coffe_Grinder_DB_Entities();
        private ObservableCollection<CoffeeSearchItem> searchResults;

        // Validation constants
        private const int MaxNameLength = 50;
        private const int MaxDescriptionLength = 200;
        private const int MaxSearchLength = 100;
        private const decimal MaxQuantity = 1000m; // 1000 kg
        private const decimal MaxPrice = 10000m; // 10000 per kg
        // Arabic letters (Unicode range \u0600-\u06FF), Latin letters, numbers, spaces, hyphens, apostrophes
        private static readonly Regex NameRegex = new Regex(@"^[\u0600-\u06FFa-zA-Z0-9\s\-']+$");
        // Arabic letters, Latin letters, numbers, spaces, hyphens
        private static readonly Regex SearchRegex = new Regex(@"^[\u0600-\u06FFa-zA-Z0-9\s\-]+$");

        // Class for search results
        public class CoffeeSearchItem
        {
            public int CoffeeID { get; set; }
            public string DisplayText { get; set; }
            public string SearchText { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        public inventory()
        {
            InitializeComponent();
            LoadCoffeeInventory();
            LoadCoffeeTypes();
            InitializeSearchBox();
            AttachEventHandlers();
            SetupInputValidation();
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                if (e.Key == Key.Down || e.Key == Key.Up)
                {
                    return;
                }

                if (comboBox.IsDropDownOpen)
                {
                    TextBox txt = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                    if (txt != null)
                    {
                        int caretIndex = txt.CaretIndex;
                        e.Handled = true;

                        if (e.Key == Key.Back && txt.Text.Length > 0 && caretIndex > 0)
                        {
                            txt.Text = txt.Text.Remove(caretIndex - 1, 1);
                            txt.CaretIndex = caretIndex - 1;
                        }
                        else if (e.Key == Key.Delete && txt.Text.Length > 0 && caretIndex < txt.Text.Length)
                        {
                            txt.Text = txt.Text.Remove(caretIndex, 1);
                            txt.CaretIndex = caretIndex;
                        }
                        else if (!string.IsNullOrEmpty(e.Key.ToString()) && e.Key.ToString().Length == 1)
                        {
                            string newChar = e.Key.ToString();
                            txt.Text = txt.Text.Insert(caretIndex, newChar);
                            txt.CaretIndex = caretIndex + 1;
                        }

                        UpdateSearchResults(txt.Text);
                    }
                }
            }
        }

        private void InitializeSearchBox()
        {
            searchResults = new ObservableCollection<CoffeeSearchItem>();
            SearchBox.ItemsSource = searchResults; // Fixed: Removed parentheses
            SearchBox.AddHandler(TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(SearchBox_TextBox_TextChanged), true);
            UpdateSearchResults(string.Empty);
        }

        private void SearchBox_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox.Template.FindName("PART_EditableTextBox", SearchBox) is TextBox textBox)
            {
                int caretIndex = textBox.CaretIndex;
                textBox.SelectionLength = 0;
                textBox.CaretIndex = caretIndex;
                UpdateSearchResults(textBox.Text);
                if (!string.IsNullOrEmpty(textBox.Text))
                {
                    SearchBox.IsDropDownOpen = true;
                }
            }
        }

        private void SetupInputValidation()
        {
            Amount.PreviewTextInput += Decimal_PreviewTextInput;
            PricePerKg.PreviewTextInput += Decimal_PreviewTextInput;
            DataObject.AddPastingHandler(Amount, OnPasteDecimalHandler);
            DataObject.AddPastingHandler(PricePerKg, OnPasteDecimalHandler);
            CoffeeName.TextChanged += CoffeeName_TextChanged;
            NewCoffeeTypeName.TextChanged += NewCoffeeTypeName_TextChanged;
            Amount.TextChanged += Amount_TextChanged;
            PricePerKg.TextChanged += PricePerKg_TextChanged;
            SearchBox.KeyUp += SearchBox_KeyUp;
            // Add LostFocus event to format Amount after editing
            Amount.LostFocus += Amount_LostFocus;
        }

        #region Validation Event Handlers
        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string text = ((TextBox)sender).Text;
            bool hasDecimalPoint = text.Contains(".");

            if (e.Text == "." && hasDecimalPoint)
            {
                e.Handled = true;
                return;
            }

            Regex regex = new Regex(@"^[0-9.]$");
            e.Handled = !regex.IsMatch(e.Text);

            // For Amount, allow up to 6 decimal places during input for flexibility
            if (sender == Amount && hasDecimalPoint && text.IndexOf(".") != -1)
            {
                int decimalPlaces = text.Substring(text.IndexOf(".") + 1).Length;
                if (decimalPlaces >= 6 && e.Text != ".")
                {
                    e.Handled = true;
                }
            }
        }

        private void OnPasteDecimalHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                // Allow up to 6 decimal places for Amount (will be converted/rounded), 2 for PricePerKg
                string pattern = (sender == Amount) ? @"^[0-9]*(?:\.[0-9]{0,6})?$" : @"^[0-9]*(?:\.[0-9]{0,2})?$";
                if (!Regex.IsMatch(text, pattern))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void CoffeeName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(textBox.Text) || textBox.Text.Length > MaxNameLength || !NameRegex.IsMatch(textBox.Text))
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
            }
            else
            {
                textBox.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void NewCoffeeTypeName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(textBox.Text) || textBox.Text.Length > MaxNameLength || !NameRegex.IsMatch(textBox.Text))
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
            }
            else
            {
                textBox.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void Amount_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Background = System.Windows.Media.Brushes.White;
                return;
            }

            if (!decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal quantity) || quantity <= 0 || quantity > MaxQuantity)
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
            }
            else
            {
                textBox.Background = System.Windows.Media.Brushes.White;
            }
        }

        private decimal ConvertQuantityInput(string input)
        {
            if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal quantity))
            {
                return 0m; // Invalid input
            }

            // Check if input has exactly 2 decimal places (e.g., "3.75")
            string[] parts = input.Split('.');
            if (parts.Length == 2 && parts[1].Length == 2)
            {
                // Interpret as kg + grams (e.g., "3.75" → 3 kg + 75 g = 3 + 0.075 = 3.075)
                int wholeKg = int.Parse(parts[0], CultureInfo.InvariantCulture);
                int grams = int.Parse(parts[1], CultureInfo.InvariantCulture);
                quantity = wholeKg + (grams / 1000m);
            }
            // Otherwise, keep as-is (e.g., "3.750", "6", "3.075" are treated literally)
            return Math.Round(quantity, 3);
        }

        private void Amount_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (string.IsNullOrEmpty(textBox.Text))
            {
                return;
            }

            decimal quantity = ConvertQuantityInput(textBox.Text);
            if (quantity <= 0 || quantity > MaxQuantity)
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
            }
            else
            {
                // Display with 3 decimal places (e.g., 3.75 → 3.075, 3.7 → 3.700, 6 → 6.000)
                textBox.Text = quantity.ToString("F3", CultureInfo.InvariantCulture);
                textBox.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void PricePerKg_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (!decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) || price <= 0 || price > MaxPrice)
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
            }
            else
            {
                textBox.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void SearchBox_KeyUp(object sender, KeyEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            if (comboBox.Text.Length > MaxSearchLength || (!string.IsNullOrEmpty(comboBox.Text) && !SearchRegex.IsMatch(comboBox.Text)))
            {
                SearchBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            if (!SearchBox.IsDropDownOpen || e.Key == Key.Enter)
            {
                UpdateSearchResults(comboBox.Text);
                if (e.Key == Key.Enter)
                {
                    FindById(sender, new RoutedEventArgs());
                }
            }
            SearchBox.Background = System.Windows.Media.Brushes.White;
        }
        #endregion

        #region Search Functionality
        private void UpdateSearchResults(string searchText)
        {
            searchResults.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                SearchBox.Background = System.Windows.Media.Brushes.White;
                SearchBox.IsDropDownOpen = false;
                return;
            }

            if (searchText.Length > MaxSearchLength || !SearchRegex.IsMatch(searchText))
            {
                SearchBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            searchText = searchText.ToLower();
            var results = db.CoffeeInventories
                .Include(c => c.CoffeeType)
                .Where(c => c.CoffeeID.ToString().Contains(searchText) ||
                           c.CoffeeName.ToLower().Contains(searchText) ||
                           (c.Description != null && c.Description.ToLower().Contains(searchText)))
                .Select(c => new
                {
                    c.CoffeeID,
                    c.CoffeeName,
                    TypeName = c.CoffeeType.TypeName,
                    Description = c.Description ?? ""
                })
                .Take(10)
                .ToList()
                .Select(c => new CoffeeSearchItem
                {
                    CoffeeID = c.CoffeeID,
                    DisplayText = $"{c.CoffeeID} - {c.CoffeeName} ({c.TypeName})",
                    SearchText = $"{c.CoffeeID} {c.CoffeeName.ToLower()} {c.Description.ToLower()}"
                })
                .ToList();

            foreach (var item in results)
            {
                searchResults.Add(item);
            }

            SearchBox.Background = results.Any() ? System.Windows.Media.Brushes.White :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
            SearchBox.IsDropDownOpen = results.Any();
        }

        private void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            if (comboBox.SelectedItem is CoffeeSearchItem selectedItem && !comboBox.IsDropDownOpen)
            {
                LoadCoffeeById(selectedItem.CoffeeID);
            }
        }

        private void LoadCoffeeById(int coffeeId)
        {
            try
            {
                var inventoryItem = db.CoffeeInventories
                    .Include(c => c.CoffeeType)
                    .FirstOrDefault(c => c.CoffeeID == coffeeId);

                if (inventoryItem == null)
                {
                    ShowErrorMessage($"لم يتم العثور على قهوة بالرقم: {coffeeId}");
                    return;
                }

                Id.Text = inventoryItem.CoffeeID.ToString();
                CoffeeName.Text = inventoryItem.CoffeeName;
                CoffeeType.SelectedValue = inventoryItem.CoffeeTypeID;
                Description.Text = inventoryItem.Description;
                // Display quantity with 3 decimal places (e.g., 3.075 for 3 kg 75 g)
                Amount.Text = Convert.ToDouble(inventoryItem.QuantityInStock).ToString("F3", CultureInfo.InvariantCulture);
                PricePerKg.Text = Convert.ToDouble(inventoryItem.PricePerKg).ToString("F2", CultureInfo.InvariantCulture);

                CoffeeDataGrid.SelectedItem = inventoryItem;
                CoffeeDataGrid.ScrollIntoView(inventoryItem);

                ShowSuccessMessage($"تم تحميل القهوة رقم {coffeeId} بنجاح.");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء البحث عن القهوة: {ex.Message}");
            }
        }

        private void FindById(object sender, RoutedEventArgs e)
        {
            if (SearchBox.SelectedItem is CoffeeSearchItem selectedItem)
            {
                LoadCoffeeById(selectedItem.CoffeeID);
                return;
            }

            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                ShowErrorMessage("يرجى إدخال رقم أو اسم أو وصف للبحث.");
                SearchBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            if (SearchBox.Text.Length > MaxSearchLength || !SearchRegex.IsMatch(SearchBox.Text))
            {
                ShowErrorMessage("يرجى إدخال نص بحث صالح (حروف عربية أو لاتينية، أرقام، مسافات، أو واصلات فقط، بحد أقصى 100 حرف).");
                SearchBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            if (int.TryParse(SearchBox.Text, out int coffeeId))
            {
                LoadCoffeeById(coffeeId);
            }
            else
            {
                var coffeeItem = db.CoffeeInventories
                    .Include(c => c.CoffeeType)
                    .FirstOrDefault(c => c.CoffeeName.ToLower().Contains(SearchBox.Text.ToLower()) ||
                                        (c.Description != null && c.Description.ToLower().Contains(SearchBox.Text.ToLower())));

                if (coffeeItem != null)
                {
                    LoadCoffeeById(coffeeItem.CoffeeID);
                }
                else
                {
                    ShowErrorMessage($"لم يتم العثور على قهوة مطابقة لـ: {SearchBox.Text}");
                    SearchBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                }
            }
        }
        #endregion

        private void AddNewCoffeeType(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewCoffeeTypeName.Text))
            {
                ShowErrorMessage("يرجى إدخال اسم نوع القهوة.");
                NewCoffeeTypeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            string newTypeName = NewCoffeeTypeName.Text.Trim();
            if (newTypeName.Length > MaxNameLength)
            {
                ShowErrorMessage($"اسم نوع القهوة يجب ألا يتجاوز {MaxNameLength} حرفًا.");
                NewCoffeeTypeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            if (!NameRegex.IsMatch(newTypeName))
            {
                ShowErrorMessage("اسم نوع القهوة يجب أن يحتوي على حروف عربية أو لاتينية، أرقام، مسافات، واصلات، أو علامات تنصيص فقط.");
                NewCoffeeTypeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                return;
            }

            try
            {
                // Check if coffee type already exists (case-insensitive)
                bool typeExists = db.CoffeeTypes
                    .Any(ct => ct.TypeName.ToLower() == newTypeName.ToLower());

                if (typeExists)
                {
                    ShowErrorMessage($"نوع القهوة '{newTypeName}' موجود بالفعل. يرجى اختيار اسم آخر.");
                    NewCoffeeTypeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                    return;
                }

                var newCoffeeType = new CoffeeType
                {
                    TypeName = newTypeName,
                };

                db.CoffeeTypes.Add(newCoffeeType);
                db.SaveChanges();

                LoadCoffeeTypes();
                NewCoffeeTypeName.Text = string.Empty;
                NewCoffeeTypeName.Background = System.Windows.Media.Brushes.White;
                ShowSuccessMessage("تمت إضافة نوع القهوة بنجاح!");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء إضافة نوع القهوة: {ex.Message}");
            }
        }

        private void DeleteCoffeeType_Click(object sender, RoutedEventArgs e)
        {
            if (CoffeeType.SelectedItem == null)
            {
                ShowErrorMessage("يرجى تحديد نوع قهوة لحذفه.");
                return;
            }

            var selectedCoffeeType = (CoffeeType)CoffeeType.SelectedItem;
            var result = MessageBox.Show($"هل أنت متأكد من حذف نوع القهوة '{selectedCoffeeType.TypeName}'؟",
                "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // Check if the coffee type is used in any inventory items
                bool isUsed = db.CoffeeInventories.Any(c => c.CoffeeTypeID == selectedCoffeeType.CoffeeTypeID);
                if (isUsed)
                {
                    ShowErrorMessage($"لا يمكن حذف نوع القهوة '{selectedCoffeeType.TypeName}' لأنه مستخدم في عناصر المخزون.");
                    return;
                }

                var coffeeType = db.CoffeeTypes.Find(selectedCoffeeType.CoffeeTypeID);
                if (coffeeType == null)
                {
                    ShowErrorMessage("لم يتم العثور على نوع القهوة المحدد في قاعدة البيانات.");
                    return;
                }

                db.CoffeeTypes.Remove(coffeeType);
                db.SaveChanges();

                LoadCoffeeTypes();
                CoffeeType.SelectedIndex = -1;
                ShowSuccessMessage($"تم حذف نوع القهوة '{selectedCoffeeType.TypeName}' بنجاح.");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء حذف نوع القهوة: {ex.Message}");
            }
        }

        private void AttachEventHandlers()
        {
            CoffeeDataGrid.SelectionChanged += (sender, e) =>
            {
                if (CoffeeDataGrid.SelectedItem is CoffeeInventory selectedItem)
                {
                    Id.Text = selectedItem.CoffeeID.ToString();
                    CoffeeName.Text = selectedItem.CoffeeName;
                    CoffeeType.SelectedValue = selectedItem.CoffeeTypeID;
                    Description.Text = selectedItem.Description;
                    // Display quantity with 3 decimal places (e.g., 3.075 for 3 kg 75 g)
                    Amount.Text = Convert.ToDouble(selectedItem.QuantityInStock).ToString("F3", CultureInfo.InvariantCulture);
                    PricePerKg.Text = Convert.ToDouble(selectedItem.PricePerKg).ToString("F2", CultureInfo.InvariantCulture);
                }
            };

            SearchBox.SelectionChanged += SearchBox_SelectionChanged;
        }

        private void LoadCoffeeTypes()
        {
            try
            {
                CoffeeType.ItemsSource = db.CoffeeTypes
                    .OrderBy(ct => ct.TypeName)
                    .ToList();
                CoffeeType.SelectedValuePath = "CoffeeTypeID";
                CoffeeType.DisplayMemberPath = "TypeName";
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء تحميل أنواع القهوة: {ex.Message}");
            }
        }

        private void refresh(object sender, RoutedEventArgs e)
        {
            LoadCoffeeInventory();
            LoadCoffeeTypes();
            ClearForm();
            SearchBox.Text = string.Empty;
            UpdateSearchResults(string.Empty);
            ShowSuccessMessage("تم تحديث المخزون بنجاح.");
        }

        private void add(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                var selectedCoffeeType = (CoffeeType)CoffeeType.SelectedItem;

                var newInventory = new CoffeeInventory
                {
                    CoffeeName = CoffeeName.Text.Trim(),
                    CoffeeTypeID = selectedCoffeeType.CoffeeTypeID,
                    // Use converted quantity (e.g., 3.75 → 3.075)
                    QuantityInStock = decimal.Parse(Amount.Text, NumberStyles.Any, CultureInfo.InvariantCulture),
                    PricePerKg = decimal.Parse(PricePerKg.Text, NumberStyles.Any, CultureInfo.InvariantCulture),
                    Description = Description.Text?.Trim()
                };

                db.CoffeeInventories.Add(newInventory);
                db.SaveChanges();

                LoadCoffeeInventory();
                ClearForm();
                UpdateSearchResults(string.Empty);
                ShowSuccessMessage("تمت إضافة القهوة إلى المخزون بنجاح.");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء إضافة القهوة: {ex.Message}");
            }
        }

        private void update(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Id.Text))
            {
                ShowErrorMessage("يرجى تحديد عنصر للتعديل.");
                return;
            }

            if (!ValidateForm()) return;

            try
            {
                int coffeeId = int.Parse(Id.Text);
                var inventory = db.CoffeeInventories.Find(coffeeId);

                if (inventory == null)
                {
                    ShowErrorMessage("لم يتم العثور على القهوة المحددة في قاعدة البيانات.");
                    return;
                }

                inventory.CoffeeName = CoffeeName.Text.Trim();
                inventory.CoffeeTypeID = (int)CoffeeType.SelectedValue;
                // Use converted quantity (e.g., 3.75 → 3.075)
                inventory.QuantityInStock = decimal.Parse(Amount.Text, NumberStyles.Any, CultureInfo.InvariantCulture);
                inventory.PricePerKg = decimal.Parse(PricePerKg.Text, NumberStyles.Any, CultureInfo.InvariantCulture);
                inventory.Description = Description.Text?.Trim();

                db.SaveChanges();
                LoadCoffeeInventory();
                ClearForm();
                UpdateSearchResults(string.Empty);
                ShowSuccessMessage("تم تعديل القهوة بنجاح.");
            }
             catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء تعديل القهوة: {ex.Message}");
            }
        }

        private void delete(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Id.Text))
            {
                ShowErrorMessage("يرجى تحديد عنصر للحذف.");
                return;
            }

            var result = MessageBox.Show("هل أنت متأكد من حذف هذه القهوة؟ سيؤدي هذا أيضًا إلى حذف جميع تفاصيل الطلبات المرتبطة.",
                "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                int coffeeId = int.Parse(Id.Text);

                var relatedOrderDetails = db.OrderDetails.Where(od => od.CoffeeID == coffeeId).ToList();
                if (relatedOrderDetails.Any())
                {
                    db.OrderDetails.RemoveRange(relatedOrderDetails);
                }

                var inventory = db.CoffeeInventories.FirstOrDefault(x => x.CoffeeID == coffeeId);
                if (inventory == null)
                {
                    ShowErrorMessage("لم يتم العثور على القهوة المحددة في قاعدة البيانات.");
                    return;
                }

                db.CoffeeInventories.Remove(inventory);
                db.SaveChanges();

                var maxId = db.CoffeeInventories.Any() ? db.CoffeeInventories.Max(c => c.CoffeeID) : 0;
                db.Database.ExecuteSqlCommand($"DBCC CHECKIDENT ('CoffeeInventory', RESEED, {maxId})");

                LoadCoffeeInventory();
                ClearForm();
                UpdateSearchResults(string.Empty);
                ShowSuccessMessage("تم حذف القهوة بنجاح. تمت إعادة ترتيب تس Croix القهوة بنجاح.");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء حذف القهوة: {ex.Message}\n\nتأكد من عدم وجود طلبات مرتبطة بهذا العنصر.");
            }
        }

        private void LoadCoffeeInventory()
        {
            try
            {
                CoffeeDataGrid.ItemsSource = db.CoffeeInventories
                    .Include(c => c.CoffeeType)
                    .OrderBy(c => c.CoffeeID)
                    .ToList();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"خطأ أثناء تحميل مخزون القهوة: {ex.Message}");
            }
        }

        private bool ValidateForm()
        {
            bool isValid = true;

            // Validate CoffeeName
            if (string.IsNullOrWhiteSpace(CoffeeName.Text))
            {
                CoffeeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage("يرجى إدخال اسم القهوة.");
                isValid = false;
            }
            else if (CoffeeName.Text.Length > MaxNameLength)
            {
                CoffeeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage($"اسم القهوة يجب ألا يتجاوز {MaxNameLength} حرفًا.");
                isValid = false;
            }
            else if (!NameRegex.IsMatch(CoffeeName.Text))
            {
                CoffeeName.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage("اسم القهوة يجب أن يحتوي على حروف عربية أو لاتينية، أرقام، مسافات، واصلات، أو علامات تنصيص فقط.");
                isValid = false;
            }
            else
            {
                CoffeeName.Background = System.Windows.Media.Brushes.White;
            }

            // Validate CoffeeType
            if (CoffeeType.SelectedItem == null)
            {
                ShowErrorMessage("يرجى تحديد نوع القهوة.");
                isValid = false;
            }

            // Validate Description
            if (!string.IsNullOrEmpty(Description.Text) && Description.Text.Length > MaxDescriptionLength)
            {
                Description.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage($"الوصف يجب ألا يتجاوز {MaxDescriptionLength} حرفًا.");
                isValid = false;
            }
            else
            {
                Description.Background = System.Windows.Media.Brushes.White;
            }

            // Validate Quantity
            if (!decimal.TryParse(Amount.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedQuantity))
            {
                Amount.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage("يرجى إدخال كمية صالحة (رقم إيجابي).");
                isValid = false;
            }
            else
            {
                decimal quantity = ConvertQuantityInput(Amount.Text);
                if (quantity <= 0)
                {
                    Amount.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                    ShowErrorMessage("يرجى إدخال كمية صالحة (رقم إيجابي).");
                    isValid = false;
                }
                else if (quantity > MaxQuantity)
                {
                    Amount.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                    ShowErrorMessage($"الكمية يجب ألا تتجاوز {MaxQuantity} كجم.");
                    isValid = false;
                }
                else
                {
                    // Update Amount.Text with converted quantity (e.g., 3.75 → 3.075)
                    Amount.Text = quantity.ToString("F3", CultureInfo.InvariantCulture);
                    Amount.Background = System.Windows.Media.Brushes.White;
                }
            }

            // Validate Price
            if (!decimal.TryParse(PricePerKg.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) || price <= 0)
            {
                PricePerKg.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage("يرجى إدخال سعر صالح (رقم إيجابي).");
                isValid = false;
            }
            else if (price > MaxPrice)
            {
                PricePerKg.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                ShowErrorMessage($"السعر يجب ألا يتجاوز {MaxPrice} لكل كجم.");
                isValid = false;
            }
            else
            {
                PricePerKg.Background = System.Windows.Media.Brushes.White;
            }

            return isValid;
        }

        private void ClearForm()
        {
            Id.Text = string.Empty;
            CoffeeName.Text = string.Empty;
            CoffeeName.Background = System.Windows.Media.Brushes.White;
            CoffeeType.SelectedIndex = -1;
            Description.Text = string.Empty;
            Description.Background = System.Windows.Media.Brushes.White;
            Amount.Text = string.Empty;
            Amount.Background = System.Windows.Media.Brushes.White;
            PricePerKg.Text = string.Empty;
            PricePerKg.Background = System.Windows.Media.Brushes.White;
            SearchBox.Text = string.Empty;
            SearchBox.Background = System.Windows.Media.Brushes.White;
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccessMessage(string message)
        {
            MessageBox.Show(message, "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}