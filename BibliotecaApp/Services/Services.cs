using System;
using System.Collections.Generic;
using System.Linq;
using BibliotecaApp.Data;
using BibliotecaApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BibliotecaApp.Services
{
    public class AuthService
    {
        private readonly LibraryContext _db;
        public User? SessionUser { get; private set; }
        public bool IsAuthenticated => SessionUser != null;

        public AuthService(LibraryContext db) => _db = db;

        public List<User> GetActiveAccounts()
            => _db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Role).ThenBy(u => u.FullName).ToList();

        public bool Authenticate(string email, string password)
        {
            var hash = HashHelper.Hash(password);
            SessionUser = _db.Users.FirstOrDefault(u => u.Email == email && u.PasswordHash == hash && u.IsActive);
            return SessionUser != null;
        }

        public void SignOut() => SessionUser = null;

        public bool HasElevatedPermissions()
            => SessionUser?.Role is UserRole.Admin or UserRole.Librarian;
    }

    public class BookService
    {
        private readonly LibraryContext _db;
        private readonly AuditService _audit;

        public BookService(LibraryContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public List<Book> GetInventory()
            => _db.Books.AsNoTracking().Include(b => b.Author).OrderBy(b => b.Title).ToList();

        public List<Book> FindBooks(string? title = null, string? author = null, string? category = null, 
                                 string? publisher = null, bool? onlyAvailable = null, BookStatus? status = null)
        {
            var query = _db.Books.AsNoTracking().Include(b => b.Author).AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title));
            
            if (!string.IsNullOrWhiteSpace(author))
                query = query.Where(b => b.Author != null && b.Author.Name.Contains(author));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(b => b.Category != null && b.Category.Contains(category));

            if (!string.IsNullOrWhiteSpace(publisher))
                query = query.Where(b => b.Publisher != null && b.Publisher.Contains(publisher));

            if (onlyAvailable == true)
                query = query.Where(b => b.AvailableCopies > 0);

            if (status.HasValue)
                query = query.Where(b => b.Status == status.Value);

            return query.OrderBy(b => b.Title).ToList();
        }

        public void Create(Book book, int executorId)
        {
            _db.Books.Add(book);
            _db.SaveChanges();
            _audit.Record("CREATE", "Books", book.Id, $"Added: {book.Title}", executorId);
        }

        public void Update(Book book, int executorId)
        {
            var tracked = _db.Books.Local.FirstOrDefault(b => b.Id == book.Id);
            if (tracked != null) _db.Entry(tracked).State = EntityState.Detached;

            _db.Books.Update(book);
            _db.SaveChanges();
            _audit.Record("UPDATE", "Books", book.Id, $"Modified: {book.Title}", executorId);
        }

        public bool Remove(int id, int executorId)
        {
            var book = _db.Books.Find(id);
            if (book == null) return false;

            if (_db.Loans.Any(l => l.BookId == id && l.Status == LoanStatus.Active))
                return false;

            var title = book.Title;
            _db.Books.Remove(book);
            _db.SaveChanges();
            _audit.Record("DELETE", "Books", id, $"Removed: {title}", executorId);
            return true;
        }
    }

    public class LoanService
    {
        private readonly LibraryContext _db;
        private readonly PenaltyService _penalties;
        private readonly AuditService _audit;

        public LoanService(LibraryContext db, PenaltyService penalties, AuditService audit)
        {
            _db = db;
            _penalties = penalties;
            _audit = audit;
        }

        public List<Loan> GetCurrentLoans()
            => _db.Loans.AsNoTracking().Include(l => l.User).Include(l => l.Book).ThenInclude(b => b!.Author)
                .OrderByDescending(l => l.BorrowedAt).ToList();

        public Loan? ProcessBorrowing(int userId, int bookId, int durationDays, int executorId)
        {
            var book = _db.Books.Find(bookId);
            if (book == null || book.AvailableCopies < 1) return null;

            var loan = new Loan
            {
                BorrowedAt = DateTime.Now,
                DueDate = DateTime.Now.AddDays(durationDays),
                Status = LoanStatus.Active,
                UserId = userId,
                BookId = bookId
            };

            book.AvailableCopies--;
            if (book.AvailableCopies == 0) book.Status = BookStatus.Borrowed;

            var user = _db.Users.Find(userId);
            _db.Loans.Add(loan);
            _db.SaveChanges();
            _audit.Record("CREATE", "Loans", loan.Id, $"Borrowed {book.Title} to {user?.FullName ?? "Unknown"}", executorId);
            return loan;
        }

        public void ProcessReturn(int id, int executorId)
        {
            var loan = _db.Loans.Include(l => l.Book).FirstOrDefault(l => l.Id == id && l.Status == LoanStatus.Active);
            if (loan == null) return;

            loan.Status = LoanStatus.Returned;
            loan.ReturnedAt = DateTime.Now;

            if (loan.Book != null)
            {
                loan.Book.AvailableCopies++;
                loan.Book.Status = BookStatus.Available;
            }

            if (DateTime.Now > loan.DueDate)
                _penalties.Apply(id, loan.DueDate);

            _db.SaveChanges();
            _audit.Record("UPDATE", "Loans", id, "Returned book", executorId);
        }
    }

    public class PenaltyService
    {
        private readonly LibraryContext _db;
        private readonly AuditService _audit;
        private const decimal LateFeePerDay = 0.50m;

        public PenaltyService(LibraryContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public List<Penalty> GetPending()
            => _db.Penalties.AsNoTracking().Include(p => p.Loan).ThenInclude(l => l!.User)
                .Include(p => p.Loan).ThenInclude(l => l!.Book)
                .Where(p => !p.IsPaid).OrderByDescending(p => p.Id).ToList();

        public void Apply(int loanId, DateTime due)
        {
            var daysLate = (int)(DateTime.Now - due).TotalDays;
            if (daysLate <= 0) return;

            IssueManual(loanId, daysLate * LateFeePerDay, $"Late return (Due: {due:dd.MM.yyyy})");
        }

        public void IssueManual(int loanId, decimal amount, string reason)
        {
            _db.Penalties.Add(new Penalty
            {
                DailyRate = LateFeePerDay,
                TotalAmount = amount,
                IsPaid = false,
                LoanId = loanId
            });
            _db.SaveChanges();
        }

        public void Resolve(int id, int executorId)
        {
            var penalty = _db.Penalties.Include(p => p.Loan).ThenInclude(l => l!.Book).FirstOrDefault(p => p.Id == id);
            if (penalty == null) return;

            penalty.IsPaid = true;
            penalty.PaidAt = DateTime.Now;
            _db.SaveChanges();
            _audit.Record("UPDATE", "Penalties", id, $"Paid fine for: {penalty.Loan?.Book?.Title}", executorId);
        }
    }

    public class UserService
    {
        private readonly LibraryContext _db;
        private readonly AuditService _audit;

        public UserService(LibraryContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public List<User> GetUserList()
            => _db.Users.AsNoTracking().OrderBy(u => u.FullName).ToList();

        public void Register(User user, string password, int executorId)
        {
            if (_db.Users.Any(u => u.Email == user.Email))
                throw new InvalidOperationException($"Email {user.Email} is already in use.");

            user.PasswordHash = HashHelper.Hash(password);
            user.RegisteredAt = DateTime.Now;
            user.IsActive = true;

            _db.Users.Add(user);
            _db.SaveChanges();
            _audit.Record("CREATE", "Users", user.Id, $"Registered: {user.Email}", executorId);
        }

        public void UpdateProfile(User user, int executorId)
        {
            if (_db.Users.Any(u => u.Email == user.Email && u.Id != user.Id))
                throw new InvalidOperationException($"Email {user.Email} is already in use.");

            var tracked = _db.Users.Local.FirstOrDefault(u => u.Id == user.Id);
            if (tracked != null) _db.Entry(tracked).State = EntityState.Detached;

            _db.Users.Update(user);
            _db.SaveChanges();
            _audit.Record("UPDATE", "Users", user.Id, $"Updated: {user.Email}", executorId);
        }

        public void Suspend(int id, int executorId)
        {
            var user = _db.Users.Find(id);
            if (user == null) return;

            user.IsActive = false;
            _db.SaveChanges();
            _audit.Record("UPDATE", "Users", id, $"Suspended: {user.Email}", executorId);
        }
    }

    public class AuditService
    {
        private readonly LibraryContext _db;
        public AuditService(LibraryContext db) => _db = db;

        public List<AuditLog> GetHistory(int limit = 50)
            => _db.AuditLogs.AsNoTracking().Include(l => l.User).OrderByDescending(l => l.Timestamp).Take(limit).ToList();

        public void Record(string action, string entity, int? entityId, string? notes, int? userId = null)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                Action = action,
                EntityName = entity,
                EntityId = entityId,
                Description = notes,
                UserId = userId
            });
            _db.SaveChanges();
        }
    }

    public class DashboardService
    {
        private readonly LibraryContext _db;
        public DashboardService(LibraryContext db) => _db = db;

        public DashboardMetrics GetMetrics()
        {
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            return new DashboardMetrics
            {
                BookCount = _db.Books.Count(),
                AvailableCount = _db.Books.Count(b => b.Status == BookStatus.Available),
                ActiveUsers = _db.Users.Count(u => u.IsActive),
                ActiveLoans = _db.Loans.Count(l => l.Status == LoanStatus.Active),
                OverdueLoans = _db.Loans.Count(l => l.Status == LoanStatus.Active && l.DueDate < now),
                UnpaidFees = _db.Penalties.Count(p => !p.IsPaid),
                MonthlyLoans = _db.Loans.Count(l => l.BorrowedAt >= monthStart)
            };
        }
    }

    public class DashboardMetrics
    {
        public int BookCount { get; set; }
        public int AvailableCount { get; set; }
        public int ActiveUsers { get; set; }
        public int ActiveLoans { get; set; }
        public int OverdueLoans { get; set; }
        public int UnpaidFees { get; set; }
        public int MonthlyLoans { get; set; }
    }

    public class NotificationService
    {
        private readonly LibraryContext _db;
        public NotificationService(LibraryContext db) => _db = db;

        public List<Alert> GetOverdueAlerts()
        {
            var now = DateTime.Now;
            return _db.Loans.AsNoTracking().Include(l => l.User).Include(l => l.Book)
                .Where(l => l.Status == LoanStatus.Active && l.DueDate < now)
                .Select(l => new Alert 
                { 
                    Message = $"{l.User!.FullName} is late returning \"{l.Book!.Title}\"",
                    DaysOverdue = (int)(now - l.DueDate).TotalDays
                }).ToList();
        }
    }

    public class Alert
    {
        public string Message { get; set; } = "";
        public int DaysOverdue { get; set; }
    }

    public class AuthorService
    {
        private readonly LibraryContext _db;
        private readonly AuditService _audit;

        public AuthorService(LibraryContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public List<Author> GetDirectory()
            => _db.Authors.AsNoTracking().OrderBy(a => a.Name).ToList();

        public void Add(Author author, int executorId)
        {
            _db.Authors.Add(author);
            _db.SaveChanges();
            _audit.Record("CREATE", "Authors", author.Id, $"Added author: {author.Name}", executorId);
        }
    }
}
