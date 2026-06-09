using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BibliotecaApp.Data;
using BibliotecaApp.Models;
using BibliotecaApp.Services;

namespace BibliotecaApp.ViewModels
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), _ => canExecute?.Invoke() ?? true) { }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
    }

    public class MainViewModel : BaseViewModel
    {
        private readonly AuthService _auth;
        private object _currentPage;

        public DashboardViewModel Dashboard { get; }
        public BooksViewModel Books { get; }
        public LoansViewModel Loans { get; }
        public UsersViewModel Users { get; }
        public PenaltiesViewModel Penalties { get; }
        public AuditViewModel Audit { get; }

        public object CurrentPage { get => _currentPage; set => SetField(ref _currentPage, value); }
        public User? CurrentUser => _auth.SessionUser;
        public bool IsStaff => _auth.HasElevatedPermissions();

        public ICommand GoToDashboard { get; }
        public ICommand GoToBooks     { get; }
        public ICommand GoToLoans     { get; }
        public ICommand GoToUsers     { get; }
        public ICommand GoToPenalties { get; }
        public ICommand GoToAudit     { get; }
        public ICommand LogoutCommand { get; }

        public event Action? LoggedOut;

        public MainViewModel(AuthService auth, DashboardViewModel dash, BooksViewModel books, 
                             LoansViewModel loans, UsersViewModel users, PenaltiesViewModel penalties, 
                             AuditViewModel audit)
        {
            _auth     = auth;
            Dashboard = dash;
            Books     = books;
            Loans     = loans;
            Users     = users;
            Penalties = penalties;
            Audit     = audit;

            _currentPage = dash;

            GoToDashboard = new RelayCommand(() => { Dashboard.Refresh(); CurrentPage = Dashboard; });
            GoToBooks     = new RelayCommand(() => { Books.Refresh();     CurrentPage = Books; });
            GoToLoans     = new RelayCommand(() => { Loans.Refresh();     CurrentPage = Loans; });
            GoToUsers     = new RelayCommand(() => { Users.Refresh();     CurrentPage = Users; });
            GoToPenalties = new RelayCommand(() => { Penalties.Refresh(); CurrentPage = Penalties; });
            GoToAudit     = new RelayCommand(() => { Audit.Refresh();     CurrentPage = Audit; });

            LogoutCommand = new RelayCommand(() => { _auth.SignOut(); LoggedOut?.Invoke(); });
        }
    }

    public class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _auth;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;

        public string Email        { get => _email;        set => SetField(ref _email, value); }
        public string Password     { get => _password;     set => SetField(ref _password, value); }
        public string ErrorMessage { get => _errorMessage; set => SetField(ref _errorMessage, value); }

        public ObservableCollection<User> Accounts { get; } = new();

        public ICommand LoginCommand { get; }
        public ICommand SelectAccountCommand { get; }
        public event Action? LoginSucceeded;

        public LoginViewModel(AuthService auth)
        {
            _auth = auth;
            LoginCommand = new RelayCommand(DoLogin, () => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password));
            
            SelectAccountCommand = new RelayCommand(obj =>
            {
                if (obj is User user)
                {
                    Email = user.Email;
                    Password = user.Email switch
                    {
                        "admin@library.com"     => "admin123",
                        "librarian@library.com" => "lib123",
                        "reader@library.com"    => "reader123",
                        _                       => string.Empty
                    };
                    ErrorMessage = string.Empty;
                }
            });

            foreach (var user in _auth.GetActiveAccounts()) Accounts.Add(user);
        }

        private void DoLogin()
        {
            ErrorMessage = string.Empty;
            if (_auth.Authenticate(Email, Password)) LoginSucceeded?.Invoke();
            else ErrorMessage = "Invalid email or password.";
        }
    }

    public class DashboardViewModel : BaseViewModel
    {
        private readonly DashboardService _service;
        private readonly NotificationService _notifications;
        private DashboardMetrics _metrics = new();

        public DashboardMetrics Stats { get => _metrics; set => SetField(ref _metrics, value); }
        public ObservableCollection<Alert> Notifications { get; } = new();

        public DashboardViewModel(DashboardService service, NotificationService notifications)
        {
            _service       = service;
            _notifications = notifications;
            Refresh();
        }

        public void Refresh()
        {
            Stats = _service.GetMetrics();
            Notifications.Clear();
            foreach (var a in _notifications.GetOverdueAlerts()) Notifications.Add(a);
        }
    }

    public class BooksViewModel : BaseViewModel
    {
        private readonly BookService _service;
        private readonly LoanService _loanService;
        private readonly AuthService _auth;
        private Book? _selected;
        private string _filterTitle = "";
        private string _filterAuthor = "";
        private string _filterCategory = "";
        private string _filterPublisher = "";
        private bool _onlyAvailable = false;

        public ObservableCollection<Book> Books { get; } = new();
        public Book? Selected { get => _selected; set => SetField(ref _selected, value); }

        public string FilterTitle     { get => _filterTitle;     set { SetField(ref _filterTitle, value);     Refresh(); } }
        public string FilterAuthor    { get => _filterAuthor;    set { SetField(ref _filterAuthor, value);    Refresh(); } }
        public string FilterCategory  { get => _filterCategory;  set { SetField(ref _filterCategory, value);  Refresh(); } }
        public string FilterPublisher { get => _filterPublisher; set { SetField(ref _filterPublisher, value); Refresh(); } }
        public bool   OnlyAvailable   { get => _onlyAvailable;   set { SetField(ref _onlyAvailable, value);   Refresh(); } }

        public bool CanManage => _auth.HasElevatedPermissions();

        public ICommand RefreshCommand { get; }
        public ICommand AddCommand     { get; }
        public ICommand EditCommand    { get; }
        public ICommand DeleteCommand  { get; }
        public ICommand BorrowCommand  { get; }

        public event Action? RequestAdd;
        public event Action<Book>? RequestEdit;
        public event Action<string>? ShowMessage;

        public BooksViewModel(BookService service, LoanService loanService, AuthService auth)
        {
            _service     = service;
            _loanService = loanService;
            _auth        = auth;

            RefreshCommand = new RelayCommand(Refresh);
            AddCommand     = new RelayCommand(() => RequestAdd?.Invoke(), () => CanManage);
            EditCommand    = new RelayCommand(() => RequestEdit?.Invoke(Selected!), () => CanManage && Selected != null);
            DeleteCommand  = new RelayCommand(DoDelete, () => CanManage && Selected != null);
            BorrowCommand  = new RelayCommand(DoBorrow, () => Selected != null && Selected.AvailableCopies > 0);

            Refresh();
        }

        public void Refresh()
        {
            var list = _service.FindBooks(FilterTitle, FilterAuthor, FilterCategory, FilterPublisher, OnlyAvailable);
            Books.Clear();
            foreach (var b in list) Books.Add(b);
        }

        private void DoBorrow()
        {
            if (Selected == null) return;
            var loan = _loanService.ProcessBorrowing(_auth.SessionUser!.Id, Selected.Id, 14, _auth.SessionUser!.Id);
            if (loan != null) Refresh();
            else ShowMessage?.Invoke("Failed to borrow book.");
        }

        public void Save(Book book)
        {
            if (book.Id == 0) _service.Create(book, _auth.SessionUser!.Id);
            else _service.Update(book, _auth.SessionUser!.Id);
            Refresh();
        }

        private void DoDelete()
        {
            if (Selected == null) return;
            if (_service.Remove(Selected.Id, _auth.SessionUser!.Id)) Refresh();
            else ShowMessage?.Invoke("Cannot delete book with active loans.");
        }
    }

    public class LoansViewModel : BaseViewModel
    {
        private readonly LoanService _service;
        private readonly PenaltyService _penaltyService;
        private readonly AuthService _auth;
        private Loan? _selected;

        public ObservableCollection<Loan> Loans { get; } = new();
        public Loan? Selected { get => _selected; set => SetField(ref _selected, value); }
        public bool CanManage => _auth.HasElevatedPermissions();

        public ICommand RefreshCommand    { get; }
        public ICommand NewLoanCommand     { get; }
        public ICommand ReturnLoanCommand { get; }
        public ICommand FineCommand       { get; }

        public event Action? RequestNew;
        public event Action<Loan, Action>? RequestReturnConfirm;
        public event Action<Loan>? RequestFine;

        public LoansViewModel(LoanService service, PenaltyService penaltyService, AuthService auth)
        {
            _service        = service;
            _penaltyService = penaltyService;
            _auth           = auth;

            RefreshCommand    = new RelayCommand(Refresh);
            NewLoanCommand     = new RelayCommand(() => RequestNew?.Invoke(), () => CanManage);
            FineCommand        = new RelayCommand(() => RequestFine?.Invoke(Selected!), () => CanManage && Selected != null);
            
            ReturnLoanCommand = new RelayCommand(
                () => RequestReturnConfirm?.Invoke(Selected!, () => DoReturn()), 
                () => Selected?.Status == LoanStatus.Active && (CanManage || Selected.UserId == _auth.SessionUser!.Id));

            Refresh();
        }

        public void Refresh()
        {
            var list = _service.GetCurrentLoans();
            if (!CanManage) list = list.Where(l => l.UserId == _auth.SessionUser!.Id).ToList();

            Loans.Clear();
            foreach (var l in list) Loans.Add(l);
        }

        public void CreateLoan(int userId, int bookId, int days)
        {
            _service.ProcessBorrowing(userId, bookId, days, _auth.SessionUser!.Id);
            Refresh();
        }

        private void DoReturn()
        {
            if (Selected == null) return;
            _service.ProcessReturn(Selected.Id, _auth.SessionUser!.Id);
            Refresh();
        }

        public void IssueFine(int loanId, decimal amount, string reason)
        {
            _penaltyService.IssueManual(loanId, amount, reason);
            Refresh();
        }
    }

    public class UsersViewModel : BaseViewModel
    {
        private readonly UserService _service;
        private readonly AuthService _auth;
        private User? _selected;

        public ObservableCollection<User> Users { get; } = new();
        public User? Selected { get => _selected; set => SetField(ref _selected, value); }

        public bool CanManage => _auth.HasElevatedPermissions();
        public bool IsAdmin   => _auth.SessionUser?.Role == UserRole.Admin;

        public ICommand RefreshCommand    { get; }
        public ICommand AddCommand        { get; }
        public ICommand EditCommand       { get; }
        public ICommand SuspendCommand    { get; }

        public event Action? RequestAdd;
        public event Action<User>? RequestEdit;
        public event Action<string>? ShowMessage;

        public UsersViewModel(UserService service, AuthService auth)
        {
            _service = service;
            _auth    = auth;

            RefreshCommand = new RelayCommand(Refresh);
            AddCommand     = new RelayCommand(() => RequestAdd?.Invoke(), () => CanManage);
            EditCommand    = new RelayCommand(() => RequestEdit?.Invoke(Selected!), () => CanManage && Selected != null);
            SuspendCommand = new RelayCommand(DoSuspend, () => IsAdmin && Selected?.IsActive == true && Selected.Id != _auth.SessionUser!.Id);

            Refresh();
        }

        public void Refresh()
        {
            var list = _service.GetUserList();
            Users.Clear();
            foreach (var u in list) Users.Add(u);
        }

        public void AddUser(User user, string password)
        {
            try { _service.Register(user, password, _auth.SessionUser!.Id); Refresh(); }
            catch (Exception ex) { ShowMessage?.Invoke(ex.Message); }
        }

        public void UpdateUser(User user)
        {
            try { _service.UpdateProfile(user, _auth.SessionUser!.Id); Refresh(); }
            catch (Exception ex) { ShowMessage?.Invoke(ex.Message); }
        }

        private void DoSuspend()
        {
            if (Selected == null) return;
            _service.Suspend(Selected.Id, _auth.SessionUser!.Id);
            Refresh();
        }
    }

    public class PenaltiesViewModel : BaseViewModel
    {
        private readonly PenaltyService _service;
        private readonly AuthService _auth;
        private Penalty? _selected;

        public ObservableCollection<Penalty> Penalties { get; } = new();
        public Penalty? Selected { get => _selected; set => SetField(ref _selected, value); }
        public bool CanManage => _auth.HasElevatedPermissions();

        public ICommand RefreshCommand { get; }
        public ICommand ResolveCommand { get; }

        public PenaltiesViewModel(PenaltyService service, AuthService auth)
        {
            _service = service;
            _auth    = auth;
            RefreshCommand = new RelayCommand(Refresh);
            ResolveCommand = new RelayCommand(DoResolve, () => Selected != null && !Selected.IsPaid && (CanManage || Selected.Loan?.UserId == _auth.SessionUser!.Id));
            Refresh();
        }

        public void Refresh()
        {
            var list = _service.GetPending();
            if (!CanManage) list = list.Where(p => p.Loan?.UserId == _auth.SessionUser!.Id).ToList();
            Penalties.Clear();
            foreach (var p in list) Penalties.Add(p);
        }

        private void DoResolve()
        {
            if (Selected == null) return;
            _service.Resolve(Selected.Id, _auth.SessionUser!.Id);
            Refresh();
        }
    }

    public class AuditViewModel : BaseViewModel
    {
        private readonly AuditService _service;
        public ObservableCollection<AuditLog> Logs { get; } = new();

        public ICommand RefreshCommand { get; }

        public AuditViewModel(AuditService service)
        {
            _service = service;
            RefreshCommand = new RelayCommand(Refresh);
            Refresh();
        }

        public void Refresh()
        {
            var list = _service.GetHistory(100);
            Logs.Clear();
            foreach (var l in list) Logs.Add(l);
        }
    }
}
