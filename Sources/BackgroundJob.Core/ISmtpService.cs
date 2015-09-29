using System;
using System.Collections.Generic;
using System.Linq;
using System.Monads;
using System.Net;
using System.Net.Mail;

namespace BackgroundJob.Core
{
    public interface ISmtpService
    {
        void Send(string subject, string body, params string[] to);

        void Send(string messageSubject, string messageBody, IEnumerable<Attachment> attachments,
            params string[] to);
    }

    public class SmtpService : ISmtpService
    {
        private readonly string _host;
        private readonly string _user;
        private readonly string _password;
        private readonly string _from;

        public SmtpService(string host, string user, string password, string @from)
        {
            _host = host.Check(s => !string.IsNullOrWhiteSpace(s), s => new ArgumentOutOfRangeException("host"));
            _from = @from.Check(s => !string.IsNullOrWhiteSpace(s), s => new ArgumentOutOfRangeException("from"));
            _user = user;
            _password = password;
        }

        public void Send(string subject, string body, params string[] to)
        {
            using (var client = new SmtpClient(_host))
            {
                if (!string.IsNullOrWhiteSpace(_user))
                    client.Credentials = new NetworkCredential(_user, _password);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_from),
                    Body = body,
                    Subject = subject,
                    IsBodyHtml = true
                };
                foreach (var mailAddress in to.Where(c=>!string.IsNullOrWhiteSpace(c)))
                {
                    mailMessage.To.Add(mailAddress);
                }
                client.Send(mailMessage);
            }
        }

        public void Send(string messageSubject, string messageBody, IEnumerable<Attachment> attachments,
            params string[] to)
        {
            using (var client = new SmtpClient(_host))
            {
                if (!string.IsNullOrWhiteSpace(_user))
                    client.Credentials = new NetworkCredential(_user, _password);

                var message = new MailMessage
                {
                    From = new MailAddress(_from),
                    Body = messageBody,
                    Subject = messageSubject,
                    IsBodyHtml = true
                };
                foreach (var mailAddress in to.Where(c=>!string.IsNullOrWhiteSpace(c)))
                {
                    message.To.Add(mailAddress);
                }
                foreach (var attachment in attachments)
                {
                    message.Attachments.Add(attachment);
                }
                client.Send(message);
            }
        }
    }
}
