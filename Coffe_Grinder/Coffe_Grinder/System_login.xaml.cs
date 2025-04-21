using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Coffe_Grinder
{
    /// <summary>
    /// Interaction logic for Cashier_login.xaml
    /// </summary>
    public partial class System_login : Page
    {
        Coffe_Grinder_DB_Entities db = new Coffe_Grinder_DB_Entities();

        public System_login()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            // Validate input fields
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("الرجاء إدخال البيانات المطلوبة!", "بيانات غير مكتملة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Query the database for the user
            var user = db.Users.Where(u => u.Username == username && u.Password == password).FirstOrDefault();

            if (user != null)
            {
                // Check if the user is a cashier
                if (user.Role == "Cashier")
                {
                    MessageBox.Show("مرحباً بك في نظام الكاشير!", "تسجيل دخول ناجح", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Store the current user ID in session or application state
                    App.Current.Properties["CurrentUserID"] = user.UserID;
                    App.Current.Properties["CurrentUsername"] = user.Username;

                    // Navigate to the cashier homepage
                    CreateOrderPage cashierPage = new CreateOrderPage();
                    this.NavigationService.Navigate(cashierPage);
                }
                else if(user.Role == "Admin")
                {
                    MessageBox.Show("مرحباً بك في نظام المدير!", "تسجيل دخول ناجح", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Store the current user ID in session or application state
                    App.Current.Properties["CurrentUserID"] = user.UserID;
                    App.Current.Properties["CurrentUsername"] = user.Username;

                    // Navigate to the cashier homepage
                    Adminhomepage adminpage = new Adminhomepage();
                    this.NavigationService.Navigate(adminpage);
                }
            }
            else
            {
                // User not found or incorrect password
                MessageBox.Show("اسم المستخدم أو كلمة المرور غير صحيحة!",
                                "خطأ في تسجيل الدخول",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void BackToMain_Click(object sender, MouseButtonEventArgs e)
        {
            // Navigate back to the main login page
            welcome mainPage = new welcome();
            this.NavigationService.Navigate(mainPage);
        }
    }
}