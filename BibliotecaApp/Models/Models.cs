using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BibliotecaApp.Models
{
    public enum UserRole { Admin, Librarian, Reader }
    public enum LoanStatus { Active, Returned }
    public enum BookStatus { Available, Borrowed }

    public class User
    {
        public int Id { get; set; }
        
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; }
        public DateTime RegisteredAt { get; set; }
        public bool IsActive { get; set; }

        public List<Loan> Loans { get; set; } = new();
        public List<AuditLog> AuditLogs { get; set; } = new();

        public override string ToString() => FullName;
    }

    public class Author
    {
        public int Id { get; set; }
        
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Nationality { get; set; }

        public string? Biography { get; set; }

        public List<Book> Books { get; set; } = new();

        public override string ToString() => Name;
    }

    public class Book
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ISBN { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(150)]
        public string? Publisher { get; set; }

        public int? PublishedYear { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public BookStatus Status { get; set; }
        public string? Description { get; set; }

        public int AuthorId { get; set; }
        public Author? Author { get; set; }

        public List<Loan> Loans { get; set; } = new();

        public override string ToString() => Title;
    }

    public class Loan
    {
        public int Id { get; set; }
        public DateTime BorrowedAt { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public LoanStatus Status { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }

        public Penalty? Penalty { get; set; }
    }

    public class Penalty
    {
        public int Id { get; set; }
        public decimal DailyRate { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidAt { get; set; }

        public int LoanId { get; set; }
        public Loan? Loan { get; set; }
    }

    public class AuditLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }

        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }
    }
}
