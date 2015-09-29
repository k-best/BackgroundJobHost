using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Monads;
using System.Text;
using System.Threading;
using BackgroundJob.Core;
using NLog;
using Quantumart.SmsSubscription.DataModel;
using Quantumart.SmsSubscription.DataModel.Managers;
using Quantumart.SmsSubscription.DataModel.Model;

namespace BackgroundJob.Jobs.ContentsAvailabilityMonitoring
{
    [ContentsAvailabilityConfigurer]
    public interface IContentsAvailabilityMonitor
    {
        void CheckAndReport(string date, CancellationToken cancellationToken);
    }

    public class ContentsAvailabilityMonitor:IContentsAvailabilityMonitor
    {
        private readonly ISmtpService _smtpService;
        private readonly Logger _logger;
        private readonly ISpecialReportManager _specialReportManager;
        private readonly ISettingsRepository _settingsRepository;
        private readonly SmsSubscriptionDataContext _context;
        private const string ReportName = "ContentSubscriptionsFailedCount";

        private const string BadContentFormat = "<a href='{0}/AdminSmsSubscriptions/Contents/EditContent/{1}'>{2}</a>: <a href='{3}/contents/{1}'>{3}/contents/{1}</a> <br />";

        private const string Subject = "Превышение пороговых значений ошибок бренд. сервисов \"Мой контент\"";

        private const string Header = "За отчетный период были превышены пороговые значения ошибок бренд. сервисов \"Мой контент\": ";

        public ContentsAvailabilityMonitor(ISmtpService smtpService, Logger logger, ISpecialReportManager specialReportManager, ISettingsRepository settingsRepository, SmsSubscriptionDataContext context)
        {
            _smtpService = smtpService;
            _logger = logger;
            _specialReportManager = specialReportManager;
            _settingsRepository = settingsRepository;
            _context = context;
        }

        public void CheckAndReport(string date, CancellationToken cancellationToken)
        {
            
            var settings = _settingsRepository.Get<ContentsAvailabilitySettings>(SubscriptionsSettingId.ContentsAvailabilityMonitoring);
            _logger.Info("Запущена проверка доступности сервисов контент провайдеров запланированная на {0}", date);
            try
            {
                var badContentsByProvider = GetBadContents(settings, cancellationToken);
                //Если ни одного плохого контента не нашлось - прекращаем выполнение.
                if (!badContentsByProvider.Any())
                    return;
                cancellationToken.ThrowIfCancellationRequested();
                //Отправляем письмо со всеми плохими контентами по дефолтным адресам.
                SendToDefaultMails(badContentsByProvider.Select(c => c.Value).ToArray(), settings);
                var providers = GetProviderEmailAddresses();
                //Отправляем каждому провайдеру у которого нашлись плохие контенты свое письмо с его плохими контентами.
                foreach (var providerBadContents in badContentsByProvider)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SendToProvider(providerBadContents.Key, providerBadContents.Value, providers);
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, ex.Message);
                throw;
            }
            _logger.Info("Проверка доступности сервисов контент провайдеров завершена");
        }

        private Dictionary<Guid, string[]> GetProviderEmailAddresses()
        {
            return _context.SMS_SUBSCRIPTION_PROVIDERs.Where(c => !c.IS_DELETED).Select(
                c =>
                    new
                    {
                        PoviderId = c.PROVIDER_ID,
                        AdditionalMails = c.AdditionalEmailsForNotifications,
                        CPMail = c.EMAIL,
                        ManagersMails =
                            c.ADMIN_USERS_PROVIDERS_RELATIONs.Where(
                                l =>
                                    l.ADMIN_USER.ADMIN_USERS_ROLES_RELATIONs.Any(
                                        r => r.ADMIN_ROLE.ID == (int) AdminRoles.Manager))
                                .Select(l => l.ADMIN_USER.EMAIL)
                    }).AsEnumerable()
                .ToDictionary(c => c.PoviderId,
                    c =>
                        c.AdditionalMails.Return(m => m.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries), Enumerable.Empty<string>())
                            .Select(m => m.Trim())
                            .Concat(c.ManagersMails ?? Enumerable.Empty<string>())
                            .Concat(string.IsNullOrWhiteSpace(c.CPMail) ? Enumerable.Empty<string>() : new[] {c.CPMail})
                            .ToArray());
        }

        private KeyValuePair<Guid, string>[] GetBadContents(ContentsAvailabilitySettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string reportName;
            var report = _specialReportManager.GetSpecalReport(ReportName, true, out reportName);
            var badContents =
                report.AsEnumerable()
                    .Select(
                        c =>
                            new BadContent
                            {
                                ProviderId = (Guid) c["PROVIDER_ID"],
                                ContentId = (Guid) c["CONTENT_ID"],
                                Prefix = (string) c["PREFIX"],
                                ErrorCount = (int) c["ERR_CNT"],
                                SuccessCount = (int) c["SUCCESS_CNT"]
                            })
                    .Where(c => c.ErrorCount > settings.AbsoluteErrorsThreshold || (c.ErrorCount/(c.ErrorCount + c.SuccessCount))*100 > settings.RelativeErrorsThreshold)
                    ;
            var groupedByProviders =
                badContents.GroupBy(c => c.ProviderId)
                    .Select(c => new KeyValuePair<Guid, string>(c.Key, CreateText(c)))
                    .ToArray();
            return groupedByProviders;
        }

        private void SendToProvider(Guid providerId, string contentsString, Dictionary<Guid, string[]> providers)
        {
            try
            {
                var body = string.Format("{0} <br /> {1}", Header, contentsString);
                string[] emails;
                if (providers.TryGetValue(providerId, out emails) && emails.Any(e => !string.IsNullOrWhiteSpace(e)))
                {
                    _smtpService.Send(Subject, body, emails.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray());
                }
                else
                {
                    _logger.Error("Не найдена информация о email адресах провайдере {0}", providerId);
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal("Не удалось отправить сообщение контент провайдеру{0}", providerId);
                _logger.Info(ex, ex.Message);
            }
        }

        private void SendToDefaultMails(string[] badContents, ContentsAvailabilitySettings settings)
        {
            if (settings.MailAddresses == null)
                _logger.Error("Не указаны адресаты по умолчанию");
            try
            {
                var body = string.Format("{0} <br /> {1}", Header, string.Join(" ", badContents));
                _smtpService.Send(Subject, body, settings.MailAddresses);
            }
            catch (Exception ex)
            {
                _logger.Fatal("Не удалось отправить сообщение адресатам по умолчанию");
                _logger.Info(ex, ex.Message);
            }
        }

        private static string CreateText(IEnumerable<BadContent> badContents)
        {
            var sb = new StringBuilder();
            foreach (var badContent in badContents)
            {
                sb.AppendLine(string.Format(BadContentFormat, JobsSettings.Default.AdminSiteUrl, badContent.ContentId,
                    badContent.Prefix, JobsSettings.Default.MyContentSiteUrl));
            }
            return sb.ToString();
        }
    }
}
