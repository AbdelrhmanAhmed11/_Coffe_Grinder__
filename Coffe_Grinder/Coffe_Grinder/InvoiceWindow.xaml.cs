using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Coffe_Grinder
{
    public partial class InvoiceWindow : Window
    {
        private readonly dynamic _orderData;

        public InvoiceWindow(dynamic orderData)
        {
            InitializeComponent();
            _orderData = orderData;
            PopulateInvoiceData();
        }

        private void PopulateInvoiceData()
        {
            try
            {
                // Populate header
                InvoiceNumber.Text = "فاتورة جديدة"; // No OrderID since it's not saved yet
                InvoiceDate.Text = $"التاريخ: {DateTime.Now:dd/MM/yyyy}";
                CustomerName.Text = $"العميل: {_orderData.CustomerName ?? "غير محدد"}";
                CustomerPhone.Text = $"رقم الهاتف: {_orderData.CustomerPhone ?? "غير محدد"}";

                // Populate order items
                InvoiceItems.ItemsSource = _orderData.OrderItems;

                // Populate total
                TotalAmount.Text = $"الإجمالي: {_orderData.OrderTotal ?? "0.00"}";

                // Populate notes
                OrderNotes.Text = _orderData.OrderNotes ?? string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل بيانات الفاتورة: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    FlowDocument document = CreatePrintableDocument();
                    document.PageHeight = printDialog.PrintableAreaHeight;
                    document.PageWidth = printDialog.PrintableAreaWidth;
                    document.PagePadding = new Thickness(40);
                    document.ColumnGap = 0;
                    document.ColumnWidth = printDialog.PrintableAreaWidth;

                    IDocumentPaginatorSource paginatorSource = document;
                    printDialog.PrintDocument(paginatorSource.DocumentPaginator, "فاتورة الطلب");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument CreatePrintableDocument()
        {
            FlowDocument document = new FlowDocument
            {
                FontFamily = new FontFamily("Traditional Arabic"),
                FontSize = 14,
                FlowDirection = FlowDirection.RightToLeft
            };

            // Header
            Paragraph header = new Paragraph(new Run("مطحنة القهوة"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            };
            document.Blocks.Add(header);

            // Invoice title
            Paragraph invoiceTitle = new Paragraph(new Run("فاتورة"))
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10, 0, 20)
            };
            document.Blocks.Add(invoiceTitle);

            // Order information
            Paragraph orderInfo = new Paragraph();
            orderInfo.Inlines.Add(new Run($"فاتورة جديدة\n"));
            orderInfo.Inlines.Add(new Run($"التاريخ: {DateTime.Now:dd/MM/yyyy}\n"));
            orderInfo.Inlines.Add(new Run($"العميل: {_orderData.CustomerName ?? "غير محدد"}\n"));
            orderInfo.Inlines.Add(new Run($"رقم الهاتف: {_orderData.CustomerPhone ?? "غير محدد"}\n"));
            orderInfo.Inlines.Add(new Run($"حالة الطلب: {_orderData.OrderStatus ?? "غير محدد"}"));
            orderInfo.FontSize = 12;
            orderInfo.Margin = new Thickness(0, 0, 0, 20);
            document.Blocks.Add(orderInfo);

            // Items table
            Table table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 0, 0, 20)
            };
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            TableRowGroup rowGroup = new TableRowGroup();
            TableRow headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("القهوة")) { FontWeight = FontWeights.Bold }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("النوع")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("الكمية (كجم)")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("السعر")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المجموع الفرعي")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
            rowGroup.Rows.Add(headerRow);

            foreach (var item in _orderData.OrderItems)
            {
                TableRow row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.CoffeeName ?? "غير محدد"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.CoffeeType ?? "غير محدد")) { TextAlignment = TextAlignment.Center }));
                row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.Quantity:F3}")) { TextAlignment = TextAlignment.Right }));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.UnitPrice.ToString("C"))) { TextAlignment = TextAlignment.Right }));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Subtotal.ToString("C"))) { TextAlignment = TextAlignment.Right }));
                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            document.Blocks.Add(table);

            // Financial details
            Paragraph total = new Paragraph(new Run($"الإجمالي: {_orderData.OrderTotal ?? "0.00"}"))
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 10)
            };
            document.Blocks.Add(total);

            Paragraph amountPaid = new Paragraph(new Run($"المبلغ المدفوع: {_orderData.AmountPaid ?? "0.00"}"))
            {
                FontSize = 12,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 10)
            };
            document.Blocks.Add(amountPaid);

            Paragraph change = new Paragraph(new Run($"الباقي: {_orderData.ChangeAmount ?? "0.00"}"))
            {
                FontSize = 12,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 20)
            };
            document.Blocks.Add(change);

            // Notes
            if (!string.IsNullOrWhiteSpace(_orderData.OrderNotes))
            {
                Paragraph notesHeader = new Paragraph(new Run("ملاحظات الطلب:"))
                {
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                document.Blocks.Add(notesHeader);

                Paragraph notesContent = new Paragraph(new Run(_orderData.OrderNotes))
                {
                    Margin = new Thickness(20, 0, 0, 0)
                };
                document.Blocks.Add(notesContent);
            }

            return document;
        }
    }

    
}