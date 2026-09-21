using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class EmailDeliveryTests
{
    [Fact]
    public async Task EmailDeliveryFailsWhenSmtpIsNotConfigured()
    {
        var service = new EmailService(Options.Create(new EmailSettings()), NullLogger<EmailService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendEmailAsync("recipient@example.com", "Subject", "<p>Body</p>"));
    }

    [Fact]
    public async Task PasswordSetupEmailIsDeliveredThroughConfiguredSmtp()
    {
        await using var smtp = new LocalSmtpServer();
        var service = CreateEmailService(smtp);

        await service.SendPasswordSetupEmailAsync(
            "new.admin@example.com",
            "Amina Khan",
            "https://app.example.com/set-password?token=activation-token",
            "Acme & Co");

        var message = await smtp.GetReceivedMessageAsync();
        Assert.Equal("new.admin@example.com", message.Recipient);
        Assert.Equal("Welcome — set up your account", DecodeEncodedWords(message.Subject));
        var passwordSetupBody = DecodeMimePart(message.Body, "html");
        Assert.Contains("https://app.example.com/set-password?token=activation-token", passwordSetupBody);
        Assert.Contains("Acme &amp; Co", passwordSetupBody);
    }

    [Fact]
    public async Task PasswordResetEmailIsDeliveredThroughConfiguredSmtp()
    {
        await using var smtp = new LocalSmtpServer();
        var service = CreateEmailService(smtp);

        await service.SendEmailAsync(
            "user@example.com",
            "Reset your password",
            "<p>Use the recovery link to reset your password.</p>");

        var message = await smtp.GetReceivedMessageAsync();
        Assert.Equal("user@example.com", message.Recipient);
        Assert.Equal("Reset your password", message.Subject);
        Assert.Contains("Use the recovery link to reset your password.", DecodeMimePart(message.Body, "html"));
    }

    [Fact]
    public async Task BillingNotificationUsesTheSelectedTemplateAndRecipient()
    {
        await using var smtp = new LocalSmtpServer();
        var sender = new BillingNotificationEmailSender(Options.Create(CreateSettings(smtp)));

        await sender.SendAsync("billing@example.com", "trial-ending", CancellationToken.None);

        var message = await smtp.GetReceivedMessageAsync();
        Assert.Equal("billing@example.com", message.Recipient);
        Assert.Equal("Your free trial ends soon", message.Subject);
        Assert.Contains("free trial ends within three days", DecodeMimePart(message.Body, "html"));
    }

    private static EmailService CreateEmailService(LocalSmtpServer smtp) =>
        new(Options.Create(CreateSettings(smtp)), NullLogger<EmailService>.Instance);

    private static EmailSettings CreateSettings(LocalSmtpServer smtp) => new()
    {
        Host = "127.0.0.1",
        Port = smtp.Port,
        EnableSsl = false,
        Username = "test-user",
        Password = "test-password",
        FromAddress = "sender@example.com",
        FromName = "Test Sender",
        SupportEmail = "support@example.com"
    };

    private sealed class LocalSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly Task serverTask;
        private readonly TaskCompletionSource<ReceivedMessage> receivedMessage =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LocalSmtpServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serverTask = Task.Run(RunAsync);
        }

        public int Port { get; }

        public async Task<ReceivedMessage> GetReceivedMessageAsync() =>
            await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            try
            {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
            {
            }
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true)
                {
                    NewLine = "\r\n"
                };
                var recipient = string.Empty;
                var data = new StringBuilder();
                var readingData = false;

                await WriteResponseAsync(writer, "220 localhost ESMTP local test");
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (readingData)
                    {
                        if (line == ".")
                        {
                            readingData = false;
                            await WriteResponseAsync(writer, "250 2.0.0 Message accepted");
                            receivedMessage.TrySetResult(ParseMessage(recipient, data.ToString()));
                            continue;
                        }
                        data.AppendLine(line);
                        continue;
                    }

                    if (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteResponseAsync(writer, "250-localhost");
                        await WriteResponseAsync(writer, "250 AUTH LOGIN");
                    }
                    else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!line.Contains(' '))
                        {
                            await WriteResponseAsync(writer, "334 VXNlcm5hbWU6");
                            _ = await reader.ReadLineAsync();
                            await WriteResponseAsync(writer, "334 UGFzc3dvcmQ6");
                            _ = await reader.ReadLineAsync();
                        }
                        await WriteResponseAsync(writer, "235 2.7.0 Authentication successful");
                    }
                    else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteResponseAsync(writer, "250 2.1.0 Sender accepted");
                    }
                    else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                    {
                        recipient = line["RCPT TO:".Length..].Trim().Trim('<', '>');
                        await WriteResponseAsync(writer, "250 2.1.5 Recipient accepted");
                    }
                    else if (line == "DATA")
                    {
                        readingData = true;
                        await WriteResponseAsync(writer, "354 End data with <CR><LF>.<CR><LF>");
                    }
                    else if (line == "QUIT")
                    {
                        await WriteResponseAsync(writer, "221 2.0.0 Closing connection");
                        break;
                    }
                    else
                    {
                        await WriteResponseAsync(writer, "250 2.0.0 OK");
                    }
                }
            }
            catch (Exception exception)
            {
                receivedMessage.TrySetException(exception);
            }
        }

        private static async Task WriteResponseAsync(StreamWriter writer, string response)
        {
            await writer.WriteLineAsync(response);
            await writer.FlushAsync();
        }

        private static ReceivedMessage ParseMessage(string recipient, string message)
        {
            var subject = string.Empty;
            using var reader = new StringReader(message);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0) break;
                if (line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                    subject = line["Subject:".Length..].Trim();
            }
            return new ReceivedMessage(recipient, subject, message);
        }
    }

    private static string DecodeEncodedWords(string value)
    {
        const string pattern = @"=\?utf-8\?B\?([A-Za-z0-9+/=]+)\?=";
        return Regex.Replace(value, pattern, match =>
            Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value)));
    }

    private static string DecodeMimePart(string message, string mediaType)
    {
        var lines = message.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith($"Content-Type: text/{mediaType}", StringComparison.OrdinalIgnoreCase))
                continue;

            var headerEnd = index + 1;
            while (headerEnd < lines.Length && !string.IsNullOrWhiteSpace(lines[headerEnd]))
                headerEnd++;

            var transferEncoding = "base64";
            for (var headerIndex = index; headerIndex < headerEnd; headerIndex++)
            {
                if (lines[headerIndex].StartsWith("Content-Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))
                    transferEncoding = lines[headerIndex].Split(':', 2)[1].Trim();
            }

            var content = new StringBuilder();
            for (var contentIndex = headerEnd + 1; contentIndex < lines.Length; contentIndex++)
            {
                if (lines[contentIndex].TrimStart().StartsWith("--", StringComparison.Ordinal))
                    break;
                content.Append(lines[contentIndex]);
            }

            var encodedContent = content.ToString().Trim();
            return transferEncoding.ToLowerInvariant() switch
            {
                "base64" => Encoding.UTF8.GetString(Convert.FromBase64String(encodedContent)),
                "quoted-printable" => DecodeQuotedPrintable(encodedContent),
                _ => encodedContent
            };
        }
        return string.Empty;
    }

    private static string DecodeQuotedPrintable(string value)
    {
        var decoded = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '=' && index + 2 < value.Length &&
                byte.TryParse(value.Substring(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var encodedByte))
            {
                decoded.Append((char)encodedByte);
                index += 2;
            }
            else if (value[index] == '=' && index + 1 < value.Length && value[index + 1] == '\n')
            {
                index++;
            }
            else
            {
                decoded.Append(value[index]);
            }
        }
        return decoded.ToString();
    }

    private sealed record ReceivedMessage(string Recipient, string Subject, string Body);
}
