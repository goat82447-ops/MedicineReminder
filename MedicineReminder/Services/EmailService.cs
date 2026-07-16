using MailKit.Net.Smtp;
using MailKit.Security;
using MedicineReminder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MedicineReminder.Services;

/// <summary>
/// Sends medicine reminder emails via Gmail SMTP using MailKit, authenticating
/// with a Gmail App Password over STARTTLS.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailOptions, ILogger<EmailService> logger)
    {
        _emailSettings = emailOptions.Value ?? throw new ArgumentNullException(nameof(emailOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendReminderEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email must not be empty.", nameof(toEmail));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Email subject must not be empty.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            throw new ArgumentException("Email HTML body must not be empty.", nameof(htmlBody));
        }

        if (string.IsNullOrWhiteSpace(plainTextBody))
        {
            throw new ArgumentException("Email plain-text body must not be empty.", nameof(plainTextBody));
        }

        using var message = BuildMessage(toEmail, subject, htmlBody, plainTextBody);
        using var client = new SmtpClient();

        try
        {
            _logger.LogInformation(
                "Connecting to SMTP server {SmtpServer}:{SmtpPort}...",
                _emailSettings.SmtpServer,
                _emailSettings.SmtpPort);

            await client.ConnectAsync(
                _emailSettings.SmtpServer,
                _emailSettings.SmtpPort,
                _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Authenticating as {SenderEmail}...", _emailSettings.SenderEmail);

            await client.AuthenticateAsync(
                _emailSettings.SenderEmail,
                _emailSettings.SenderPassword,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Sending reminder email to {ReceiverEmail}...", toEmail);

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Reminder email sent successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Sending the reminder email was cancelled or timed out.");
            throw;
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "SMTP authentication failed. Verify the Gmail App Password and sender email.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while sending the reminder email.");
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private MimeMessage BuildMessage(string toEmail, string subject, string htmlBody, string plainTextBody)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_emailSettings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = plainTextBody,
        };
        message.Body = bodyBuilder.ToMessageBody();

        return message;
    }
}
