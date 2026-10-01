using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Services;

/// <summary>
/// Sends due email_notifications rows, one row per transaction. A row is due when it is Queued and
/// its next_attempt_at is unset or has passed (the ix_email_due index). Each row is locked
/// <c>FOR UPDATE SKIP LOCKED</c> before sending, so two pollers (or two API instances) never send
/// the same row twice, and the status change commits together with the attempt:
/// <list type="bullet">
/// <item>success: Sent, sent_at, provider_message_id;</item>
/// <item>failure: attempt_count + 1 and last_error, then next_attempt_at backs off
/// (RetryBaseSeconds × 2^(attempt − 1)); the attempt that reaches max_attempts makes it Failed;</item>
/// <item>a row that can never render (its booking request is gone): Failed at once.</item>
/// </list>
/// </summary>
public sealed class EmailDispatcher(
    StudyHiveDbContext db,
    EmailRenderer renderer,
    IEmailProvider provider,
    IOptions<BrevoOptions> options,
    TimeProvider clock,
    ILogger<EmailDispatcher> logger)
{
    /// <summary>The due rows as of <paramref name="now"/>.</summary>
    public static IQueryable<EmailNotification> Due(IQueryable<EmailNotification> emails, DateTimeOffset now) =>
        emails.Where(e => e.Status == EmailNotificationStatus.Queued && (e.NextAttemptAt == null || e.NextAttemptAt <= now));

    /// <summary>Sends up to BatchSize due rows, oldest first. Returns how many were attempted.</summary>
    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var ids = await Due(db.EmailNotifications.AsNoTracking(), clock.GetUtcNow())
            .OrderBy(e => e.CreatedAt)
            .Select(e => e.Id)
            .Take(Math.Max(1, options.Value.BatchSize))
            .ToListAsync(ct);

        var attempted = 0;
        foreach (var id in ids)
        {
            if (await SendOneAsync(id, ct)) attempted++;
        }
        return attempted;
    }

    /// <summary>Attempts one row if it is still due and no one else holds it. Returns false when it
    /// was skipped (already sent, not yet due, or locked by another sender).</summary>
    public async Task<bool> SendOneAsync(Guid emailId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Not composed further, so EF sends the locking query exactly as written.
        var email = (await db.EmailNotifications
            .FromSqlInterpolated($"""
                SELECT * FROM email_notifications
                WHERE id = {emailId} AND status = 'Queued' AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct))
            .SingleOrDefault();
        if (email is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }

        var message = await renderer.RenderAsync(email, ct);
        if (message is null)
        {
            email.AttemptCount++;
            email.LastError = "Nothing to render: the booking request or the data this template needs no longer exists.";
            email.Status = EmailNotificationStatus.Failed;
            email.NextAttemptAt = null;
        }
        else
        {
            EmailSendResult result;
            try
            {
                result = await provider.SendAsync(message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Counted as an attempt, so a provider that throws still ends Failed after max_attempts.
                result = EmailSendResult.Failed($"{ex.GetType().Name}: {ex.Message}");
            }
            email.AttemptCount++;
            if (result.Succeeded)
            {
                email.Status = EmailNotificationStatus.Sent;
                email.SentAt = clock.GetUtcNow();
                email.ProviderMessageId = result.ProviderMessageId;
                email.LastError = null;
                email.NextAttemptAt = null;
            }
            else
            {
                email.LastError = result.Error;
                if (email.AttemptCount >= email.MaxAttempts)
                {
                    email.Status = EmailNotificationStatus.Failed;
                    email.NextAttemptAt = null;
                }
                else
                {
                    var delay = TimeSpan.FromSeconds(options.Value.RetryBaseSeconds * Math.Pow(2, email.AttemptCount - 1));
                    email.NextAttemptAt = now + delay;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (email.Status == EmailNotificationStatus.Sent)
        {
            logger.LogInformation("Sent email {EmailId} ({Template})", email.Id, email.Template);
        }
        else
        {
            logger.LogWarning("Email {EmailId} ({Template}) attempt {Attempt}/{MaxAttempts} failed, now {Status}: {Error}",
                email.Id, email.Template, email.AttemptCount, email.MaxAttempts, email.Status, email.LastError);
        }
        return true;
    }
}

/// <summary>Polls for due emails every PollIntervalSeconds, each poll in its own DI scope. Only
/// registered when Brevo is configured (see <see cref="EmailSenderRegistration"/>).</summary>
public sealed class EmailSenderService(
    IServiceScopeFactory scopeFactory,
    IOptions<BrevoOptions> options,
    ILogger<EmailSenderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds)));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().SendDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A database blip must not kill the sender; the rows stay Queued for the next poll.
                logger.LogError(ex, "Email sender poll failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static class EmailSenderRegistration
{
    /// <summary>Registers the Brevo sender only when an API key and sender address are configured.
    /// Without them (dev, CI, tests) nothing that sends is registered, queued rows stay Queued, and
    /// startup never fails for lack of email. Returns whether the sender was registered.</summary>
    public static bool AddEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(BrevoOptions.SectionName);
        services.Configure<BrevoOptions>(section);

        var brevo = section.Get<BrevoOptions>() ?? new BrevoOptions();
        if (!brevo.IsConfigured) return false;

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<EmailRenderer>();
        services.AddScoped<EmailDispatcher>();
        services.AddHttpClient<IEmailProvider, BrevoEmailProvider>(client =>
        {
            client.BaseAddress = new Uri(brevo.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("api-key", brevo.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        });
        services.AddHostedService<EmailSenderService>();
        return true;
    }
}
