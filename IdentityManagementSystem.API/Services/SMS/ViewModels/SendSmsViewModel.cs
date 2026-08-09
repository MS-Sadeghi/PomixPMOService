using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.API.Services.SMS.ViewModels
{
    public class SendSmsViewModel
    {
        [Required]
        public string Text { get; set; }
        [Required]
        public string LogText { get; set; }

        private string _mobile;
        [Required]
        public string Mobile
        {
            get => _mobile;
            set => _mobile = NormalizeMobileNumber(value);
        }

        public long? UserId { get; set; }

        [Required]
        public string Area { get; set; } = null!;

        [Required]
        public string Controller { get; set; } = null!;

        [Required]
        public string Action { get; set; } = null!;

        /// <summary>
        /// نرمال‌سازی شماره موبایل به فرمت 0098
        /// </summary>
        /// <param name="mobile">شماره موبایل ورودی</param>
        /// <returns>شماره موبایل نرمال‌شده</returns>
        public static string NormalizeMobileNumber(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return mobile;
            }

            // حذف فاصله‌ها، خط تیره‌ها و سایر کاراکترهای غیرعددی به جز +
            var cleanedMobile = new string(mobile.Where(c => char.IsDigit(c) || c == '+').ToArray());

            // حذف + در صورت وجود
            cleanedMobile = cleanedMobile.Replace("+", "");

            // بررسی فرمت‌های مختلف
            if (cleanedMobile.StartsWith("0098"))
            {
                // فرمت 00989137313070 صحیح است
                return cleanedMobile.Length == 13 ? cleanedMobile : throw new ArgumentException("شماره موبایل نامعتبر است.");
            }
            else if (cleanedMobile.StartsWith("98") && cleanedMobile.Length == 12)
            {
                // فرمت +989137313070 یا 989137313070
                return "00" + cleanedMobile;
            }
            else if (cleanedMobile.StartsWith("0") && cleanedMobile.Length == 11)
            {
                // فرمت 09137313070
                return "0098" + cleanedMobile.Substring(1);
            }
            else if (cleanedMobile.Length == 10)
            {
                // فرمت 9137313070
                return "0098" + cleanedMobile;
            }

            throw new ArgumentException("فرمت شماره موبایل نامعتبر است.");
        }
    }
}
