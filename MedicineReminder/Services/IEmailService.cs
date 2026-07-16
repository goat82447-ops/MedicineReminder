namespace MedicineReminder.Services;

/// <summary>
/// Abstraction for sending reminder emails, enabling dependency injection and unit testing.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends the medicine reminder email asynchronously as a styled HTML
    /// message with a plain-text fallback for clients that don't render HTML.
    /// </summary>
    /// <param name="toEmail">Recipient email address (per-reminder override or the configured default).</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML email body.</param>
    /// <param name="plainTextBody">Plain-text fallback body.</param>
    /// <param name="cancellationToken">Token used to cancel the send operation.</param>
    Task SendReminderEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default);
}
