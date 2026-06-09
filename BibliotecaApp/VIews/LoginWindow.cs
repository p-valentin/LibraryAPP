using System.Windows;
using BibliotecaApp.ViewModels;

namespace BibliotecaApp.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        public LoginWindow(LoginViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            TxtPassword.PasswordChanged += (_, _) => _vm.Password = TxtPassword.Password;

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.Password) && TxtPassword.Password != vm.Password)
                {
                    TxtPassword.Password = vm.Password ?? string.Empty;
                }
            };

            vm.LoginSucceeded += () =>
            {
                var main = App.ServiceLocator.GetMainWindow();
                main.Show();
                Close();
            };
        }
    }
}
