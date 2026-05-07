using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CineFlow.Models.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class StrongPasswordAttribute : ValidationAttribute
    {
        public StrongPasswordAttribute()
        {
            ErrorMessage = "Şifre en az 8 karakter olmalı; en az 1 büyük harf ve 1 sembol içermelidir.";
        }

        public override bool IsValid(object? value)
        {
            if (value is null) return true;
            if (value is not string s) return false;

            s = s.Trim();
            if (s.Length == 0) return true;

            if (s.Length < 8) return false;
            if (!s.Any(char.IsUpper)) return false;
            if (!s.Any(ch => !char.IsLetterOrDigit(ch))) return false;

            return true;
        }
    }
}

