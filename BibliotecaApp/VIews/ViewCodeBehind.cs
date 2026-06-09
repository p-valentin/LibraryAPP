using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BibliotecaApp.Models;

namespace BibliotecaApp.Views
{
    public class AddBookDialog : Window
    {
        private readonly Book? _existing;
        private readonly List<Author> _authorsList;

        private readonly TextBox _txtTitle = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtAuthor = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtISBN = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtCategory = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtPublisher = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtYear = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtCopies = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtDescription = new() { Style = (Style)Application.Current.Resources["InputStyle"], Height = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };

        public Book? Result { get; private set; }

        public AddBookDialog(Book? existing = null)
        {
            _existing = existing;
            _authorsList = App.ServiceLocator.GetAllAuthors();

            Title = existing == null ? "Add Book" : "Edit Book";
            Width = 450; Height = 650;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 42, 56));
            Foreground = Brushes.White;

            var mainStack = new StackPanel { Margin = new Thickness(30) };

            mainStack.Children.Add(CreateField("Title *", _txtTitle));
            mainStack.Children.Add(CreateField("Author *", _txtAuthor));
            mainStack.Children.Add(CreateField("ISBN", _txtISBN));
            mainStack.Children.Add(CreateField("Category", _txtCategory));
            mainStack.Children.Add(CreateField("Publisher", _txtPublisher));
            mainStack.Children.Add(CreateField("Published Year", _txtYear));
            mainStack.Children.Add(CreateField("Total Copies", _txtCopies));
            mainStack.Children.Add(CreateField("Description", _txtDescription));

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var btnSave = new Button { Content = "Save", Style = (Style)Application.Current.Resources["PrimaryButton"], Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Style = (Style)Application.Current.Resources["SecondaryButton"], Width = 100, Height = 35 };

            btnSave.Click += (_, _) => DoSave();
            btnCancel.Click += (_, _) => DialogResult = false;

            btnStack.Children.Add(btnSave);
            btnStack.Children.Add(btnCancel);
            mainStack.Children.Add(btnStack);

            Content = new ScrollViewer { Content = mainStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            if (existing != null)
            {
                _txtTitle.Text = existing.Title;
                _txtAuthor.Text = existing.Author?.Name ?? "";
                _txtISBN.Text = existing.ISBN ?? "";
                _txtCategory.Text = existing.Category ?? "";
                _txtPublisher.Text = existing.Publisher ?? "";
                _txtYear.Text = existing.PublishedYear?.ToString() ?? "";
                _txtCopies.Text = existing.TotalCopies.ToString();
                _txtDescription.Text = existing.Description ?? "";
            }
        }

        private FrameworkElement CreateField(string label, FrameworkElement control)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            stack.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 197)), FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            stack.Children.Add(control);
            return stack;
        }

        private void DoSave()
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text)) { MessageBox.Show("Title is required."); return; }
            if (string.IsNullOrWhiteSpace(_txtAuthor.Text)) { MessageBox.Show("Author is required."); return; }

            int? year = int.TryParse(_txtYear.Text, out int y) ? y : null;
            int total = int.TryParse(_txtCopies.Text, out int n) ? Math.Max(1, n) : 1;

            int oldTotal = _existing?.TotalCopies ?? 0;
            int oldAvailable = _existing?.AvailableCopies ?? total;
            int diff = total - oldTotal;
            int newAvailable = Math.Clamp(oldAvailable + diff, 0, total);

            var authorName = _txtAuthor.Text.Trim();
            var author = _authorsList.FirstOrDefault(a => string.Equals(a.Name, authorName, StringComparison.OrdinalIgnoreCase))
                         ?? new Author { Id = 0, Name = authorName };

            Result = new Book
            {
                Id = _existing?.Id ?? 0,
                Title = _txtTitle.Text.Trim(),
                AuthorId = author.Id,
                Author = author,
                ISBN = _txtISBN.Text.Blank(),
                Category = _txtCategory.Text.Blank(),
                Publisher = _txtPublisher.Text.Blank(),
                PublishedYear = year,
                TotalCopies = total,
                AvailableCopies = newAvailable,
                Status = newAvailable > 0 ? BookStatus.Available : BookStatus.Borrowed,
                Description = _txtDescription.Text.Blank()
            };
            DialogResult = true;
        }
    }

    public class NewLoanDialog : Window
    {
        private readonly ComboBox _cmbUser = new() { Style = (Style)Application.Current.Resources["ComboBoxStyle"] };
        private readonly ComboBox _cmbBook = new() { Style = (Style)Application.Current.Resources["ComboBoxStyle"] };
        private readonly TextBox _txtDays = new() { Style = (Style)Application.Current.Resources["InputStyle"], Text = "14" };

        public int UserId => (_cmbUser.SelectedItem as User)?.Id ?? 0;
        public int BookId => (_cmbBook.SelectedItem as Book)?.Id ?? 0;
        public int Days => int.TryParse(_txtDays.Text, out int d) ? d : 14;

        public NewLoanDialog()
        {
            Title = "New Loan";
            Width = 400; Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 42, 56));
            Foreground = Brushes.White;

            var stack = new StackPanel { Margin = new Thickness(30) };

            _cmbUser.DisplayMemberPath = "FullName";
            _cmbUser.ItemsSource = App.ServiceLocator.GetActiveUsers();

            _cmbBook.DisplayMemberPath = "Title";
            _cmbBook.ItemsSource = App.ServiceLocator.GetAvailableBooks();

            stack.Children.Add(CreateField("Select User", _cmbUser));
            stack.Children.Add(CreateField("Select Book", _cmbBook));
            stack.Children.Add(CreateField("Duration (days)", _txtDays));

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var btnOk = new Button { Content = "Confirm", Style = (Style)Application.Current.Resources["SuccessButton"], Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Style = (Style)Application.Current.Resources["SecondaryButton"], Width = 100, Height = 35 };

            btnOk.Click += (_, _) => DialogResult = true;
            btnCancel.Click += (_, _) => DialogResult = false;

            btnStack.Children.Add(btnOk);
            btnStack.Children.Add(btnCancel);
            stack.Children.Add(btnStack);

            Content = stack;
        }

        private FrameworkElement CreateField(string label, FrameworkElement control)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            s.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 197)), FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            s.Children.Add(control);
            return s;
        }
    }

    public class AddUserDialog : Window
    {
        private readonly User? _existing;
        private readonly TextBox _txtName = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtEmail = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly TextBox _txtPass = new() { Style = (Style)Application.Current.Resources["InputStyle"] };
        private readonly ComboBox _cmbRole = new() { Style = (Style)Application.Current.Resources["ComboBoxStyle"] };

        public User? Result { get; private set; }
        public string PlainPassword => _txtPass.Text;

        public AddUserDialog(User? existing = null)
        {
            _existing = existing;
            Title = existing == null ? "New Account" : "Edit Account";
            Width = 400; Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 42, 56));
            Foreground = Brushes.White;

            var stack = new StackPanel { Margin = new Thickness(30) };

            _cmbRole.ItemsSource = Enum.GetValues(typeof(UserRole));

            stack.Children.Add(CreateField("Full Name", _txtName));
            stack.Children.Add(CreateField("Email Address", _txtEmail));
            
            if (existing == null)
                stack.Children.Add(CreateField("Password", _txtPass));

            stack.Children.Add(CreateField("Access Level", _cmbRole));

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var btnSave = new Button { Content = "Save", Style = (Style)Application.Current.Resources["PrimaryButton"], Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Style = (Style)Application.Current.Resources["SecondaryButton"], Width = 100, Height = 35 };

            btnSave.Click += (_, _) => DoSave();
            btnCancel.Click += (_, _) => DialogResult = false;

            btnStack.Children.Add(btnSave);
            btnStack.Children.Add(btnCancel);
            stack.Children.Add(btnStack);

            Content = stack;

            if (existing != null)
            {
                _txtName.Text = existing.FullName;
                _txtEmail.Text = existing.Email;
                _cmbRole.SelectedItem = existing.Role;
            }
            else
            {
                _cmbRole.SelectedIndex = 2; // Default to Reader
            }
        }

        private FrameworkElement CreateField(string label, FrameworkElement control)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            s.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 197)), FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            s.Children.Add(control);
            return s;
        }

        private void DoSave()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text)) { MessageBox.Show("Name is required."); return; }
            if (string.IsNullOrWhiteSpace(_txtEmail.Text)) { MessageBox.Show("Email is required."); return; }
            if (_existing == null && string.IsNullOrWhiteSpace(_txtPass.Text)) { MessageBox.Show("Password is required."); return; }

            Result = new User
            {
                Id = _existing?.Id ?? 0,
                FullName = _txtName.Text.Trim(),
                Email = _txtEmail.Text.Trim(),
                Role = (UserRole)_cmbRole.SelectedItem,
                IsActive = _existing?.IsActive ?? true,
                RegisteredAt = _existing?.RegisteredAt ?? DateTime.Now,
                PasswordHash = _existing?.PasswordHash ?? ""
            };
            DialogResult = true;
        }
    }

    public class IssueFineDialog : Window
    {
        private readonly TextBox _txtAmount = new() { Style = (Style)Application.Current.Resources["InputStyle"], Text = "5.00" };
        private readonly TextBox _txtReason = new() { Style = (Style)Application.Current.Resources["InputStyle"], Tag = "Reason for fine..." };

        public decimal Amount => decimal.TryParse(_txtAmount.Text, out decimal a) ? a : 0;
        public string Reason => _txtReason.Text.Trim();

        public IssueFineDialog(Loan loan)
        {
            Title = $"Issue Fine - {loan.Book?.Title}";
            Width = 400; Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 42, 56));
            Foreground = Brushes.White;

            var stack = new StackPanel { Margin = new Thickness(30) };

            stack.Children.Add(CreateField($"Reader: {loan.User?.FullName}", new TextBlock { Text = "Set fine details below:", Foreground = Brushes.Gray }));
            stack.Children.Add(CreateField("Amount (USD)", _txtAmount));
            stack.Children.Add(CreateField("Reason", _txtReason));

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnOk = new Button { Content = "Issue", Style = (Style)Application.Current.Resources["DangerButton"], Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Style = (Style)Application.Current.Resources["SecondaryButton"], Width = 100, Height = 35 };

            btnOk.Click += (_, _) => { if (Amount <= 0) MessageBox.Show("Enter a valid amount."); else DialogResult = true; };
            btnCancel.Click += (_, _) => DialogResult = false;

            btnStack.Children.Add(btnOk);
            btnStack.Children.Add(btnCancel);
            stack.Children.Add(btnStack);

            Content = stack;
        }

        private FrameworkElement CreateField(string label, FrameworkElement control)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            s.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 197)), FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            s.Children.Add(control);
            return s;
        }
    }

    internal static class StringExtensions
    {
        public static string? Blank(this string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
