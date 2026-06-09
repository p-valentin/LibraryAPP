using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BibliotecaApp.Data;
using BibliotecaApp.Models;
using BibliotecaApp.Services;
using BibliotecaApp.ViewModels;
using BibliotecaApp.Views;

namespace BibliotecaApp
{
    public partial class App : Application
    {
        public static ServiceLocator ServiceLocator { get; } = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            var login = new LoginWindow(ServiceLocator.GetLoginViewModel());
            login.Show();
        }
    }

    public class ServiceLocator
    {
        private readonly LibraryContext _db;
        private readonly AuthService _auth;
        private readonly AuditService _audit;
        private readonly BookService _books;
        private readonly LoanService _loans;
        private readonly PenaltyService _penalties;
        private readonly UserService _users;
        private readonly DashboardService _dashboard;
        private readonly NotificationService _notifications;
        private readonly AuthorService _authors;

        public ServiceLocator()
        {
            _db = new LibraryContext();
            LibraryContext.Initialize(_db);

            _audit         = new AuditService(_db);
            _auth          = new AuthService(_db);
            _authors       = new AuthorService(_db, _audit);
            _books         = new BookService(_db, _audit);
            _penalties     = new PenaltyService(_db, _audit);
            _loans         = new LoanService(_db, _penalties, _audit);
            _users         = new UserService(_db, _audit);
            _dashboard     = new DashboardService(_db);
            _notifications = new NotificationService(_db);
        }

        public LoginViewModel GetLoginViewModel() => new(_auth);

        public System.Windows.Window GetMainWindow()
        {
            var dash      = new DashboardViewModel(_dashboard, _notifications);
            var books     = new BooksViewModel(_books, _loans, _auth);
            var loans     = new LoansViewModel(_loans, _penalties, _auth);
            var users     = new UsersViewModel(_users, _auth);
            var penalties = new PenaltiesViewModel(_penalties, _auth);
            var audit     = new AuditViewModel(_audit);

            var vm = new MainViewModel(_auth, dash, books, loans, users, penalties, audit);
            return new MainWindow(vm);
        }

        public List<Author> GetAllAuthors() => _authors.GetDirectory();
        public List<User>   GetActiveUsers() => _users.GetUserList().Where(u => u.IsActive).ToList();
        public List<Book>   GetAvailableBooks() => _books.GetInventory().Where(b => b.AvailableCopies > 0).ToList();
    }
}
