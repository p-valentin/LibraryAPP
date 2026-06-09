using System.Collections.Generic;
using System.Windows;
using BibliotecaApp.Models;
using BibliotecaApp.ViewModels;
using BibliotecaApp.Views;

namespace BibliotecaApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            vm.Books.RequestAdd     += OnBooksAdd;
            vm.Books.RequestEdit    += OnBooksEdit;
            vm.Books.ShowMessage    += msg => MessageBox.Show(msg, "Library", MessageBoxButton.OK, MessageBoxImage.Warning);
            
            vm.Loans.RequestNew            += OnLoansNew;
            vm.Loans.RequestReturnConfirm += (loan, onConfirm) =>
            {
                var msg = $"Return \"{loan.Book?.Title}\" now?";
                if (MessageBox.Show(msg, "Return Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    onConfirm();
            };
            vm.Loans.RequestFine += (loan) =>
            {
                var dlg = new IssueFineDialog(loan) { Owner = this };
                if (dlg.ShowDialog() == true)
                    _vm.Loans.IssueFine(loan.Id, dlg.Amount, dlg.Reason);
            };
            
            vm.Users.RequestAdd     += OnUsersAdd;
            vm.Users.RequestEdit    += OnUsersEdit;
            vm.Users.ShowMessage    += msg => MessageBox.Show(msg, "Library", MessageBoxButton.OK, MessageBoxImage.Warning);

            vm.LoggedOut += () =>
            {
                var login = new LoginWindow(App.ServiceLocator.GetLoginViewModel());
                login.Show();
                Close();
            };
        }

        private void OnBooksAdd()
        {
            var dlg = new AddBookDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                _vm.Books.Save(dlg.Result);
        }

        private void OnBooksEdit(Book book)
        {
            var dlg = new AddBookDialog(book) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                _vm.Books.Save(dlg.Result);
        }

        private void OnLoansNew()
        {
            var dlg = new NewLoanDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.UserId > 0 && dlg.BookId > 0)
                _vm.Loans.CreateLoan(dlg.UserId, dlg.BookId, dlg.Days);
        }

        private void OnUsersAdd()
        {
            var dlg = new AddUserDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                _vm.Users.AddUser(dlg.Result, dlg.PlainPassword);
        }

        private void OnUsersEdit(User user)
        {
            var dlg = new AddUserDialog(user) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                _vm.Users.UpdateUser(dlg.Result);
        }
    }
}
