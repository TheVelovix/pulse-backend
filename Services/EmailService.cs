using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace pulse.Services;

public class EmailService
{
    private readonly string _host = Environment.GetEnvironmentVariable("SMTP_HOST")!;
    private readonly int _port = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT")!);
    private readonly string _user = Environment.GetEnvironmentVariable("SMTP_USER")!;
    private readonly string _pass = Environment.GetEnvironmentVariable("SMTP_PASSWORD")!;

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Pulse", _user));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_user, _pass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
