using System.ComponentModel.DataAnnotations;

namespace MedicineReminder.Models;

/// <summary>
/// Gmail SMTP configuration used to authenticate and send reminder emails.
/// Values are bound from the "EmailSettings" section of appsettings.json
/// and can be overridden by environment variables (e.g. GitHub Secrets)
/// using the "EmailSettings__PropertyName" convention.
/// </summary>
public sealed class EmailSettings
{
    [Required(ErrorMessage = "SMTP server is required.")]
    public string SmtpServer { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "SMTP port must be between 1 and 65535.")]
    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    [Required(ErrorMessage = "Sender email is required.")]
    [EmailAddress(ErrorMessage = "Sender email is not a valid email address.")]
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gmail App Password (NOT the Gmail account password). Generate one at
    /// https://myaccount.google.com/apppasswords after enabling 2-Step Verification.
    /// </summary>
    [Required(ErrorMessage = "Sender password (Gmail App Password) is required.")]
    public string SenderPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Receiver email is required.")]
    [EmailAddress(ErrorMessage = "Receiver email is not a valid email address.")]
    public string ReceiverEmail { get; set; } = string.Empty;
}
