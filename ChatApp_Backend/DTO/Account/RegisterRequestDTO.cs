using System.ComponentModel.DataAnnotations;

namespace ChatApp_Backend.DTO.Account
{
    public class RegisterRequestDTO
    {
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        ErrorMessage = "Invalid email format")]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,}$", ErrorMessage = "Enter a Strong Password contains at least 8 Upper, Lower, Numric and Special Chars")]
        public string Password { get; set; }
        [Required]
        [DataType(DataType.Password)]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,}$",ErrorMessage = "Enter a Strong Password contains at least 8 Upper, Lower, Numric and Special Chars")]
        [Compare(nameof(Password), ErrorMessage = "Password and Confiremed Password Should be the same")]
        public string ConfirmPassword { get; set; }

    }
}
