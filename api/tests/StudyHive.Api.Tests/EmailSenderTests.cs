using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// The S3 email sender. Rows are queued for real by an approval through the API, then sent with
/// <see cref="EmailDispatcher.SendOneAsync"/> on exactly that row, through a fake provider and a
/// fake clock. Tests never call SendDueAsync: API test classes share one database, and a batch send
/// would pick up (and change) other tests' Queued rows. The test host has no Brevo key, so its own
/// background sender is never registered either.
/// </summary>
public class EmailSenderTests(ApprovalsFixture fx) : IClassFixture<ApprovalsFixture>
{
    private static int nextDayOffset = 400;
    private static int NextDays(int sessions) => Interlocked.Add(ref nextDayOffset, sessions + 1);

    [Fact]
    public async Task An_Approved_Email_Is_Rendered_From_Current_Data_Sent_Once_And_Marked_Sent()
    {
        var roomId = await fx.SeedRoomAsync(hourlyRate: 100m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 2, [], NextDays(2));
        await DecideAsync(proposal.QuotationId, "Approved", "Bring your student ID.");

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var email = await QueuedEmailAsync(db, proposal.RequestId);
        var provider = new FakeEmailProvider();
        var clock = new FakeClock(DateTimeOffset.UtcNow);

        (await Dispatcher(db, provider, clock).SendOneAsync(email.Id, CancellationToken.None)).Should().BeTrue();

        var message = provider.Sent.Should().ContainSingle().Subject;
        message.ToEmail.Should().Be(email.ToEmail);
        message.Subject.Should().Be("Your StudyHive booking is approved");
        var room = await db.StudyRooms.AsNoTracking().SingleAsync(r => r.Id == roomId);
        var quotation = await db.Quotations.AsNoTracking().SingleAsync(q => q.Id == proposal.QuotationId);
        var firstStart = proposal.Slots[0].StartsAt.ToOffset(TimeSpan.FromMinutes(330));
        message.TextBody.Should()
            .Contain(room.Name)
            .And.Contain($"{firstStart.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture)}, 09:00–10:30")
            .And.Contain($"Total: LKR {quotation.TotalAmount.ToString("N2", CultureInfo.InvariantCulture)}")
            .And.Contain("Librarian's comment: Bring your student ID.");
        message.HtmlBody.Should().Contain("<p>").And.Contain(WebUtility.HtmlEncode(room.Name));

        var row = await ReloadAsync(db, email.Id);
        row.Status.Should().Be(EmailNotificationStatus.Sent);
        row.ProviderMessageId.Should().Be("fake-1");
        row.SentAt.Should().BeCloseTo(clock.Now, TimeSpan.FromMilliseconds(1));
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().BeNull();
        row.NextAttemptAt.Should().BeNull();

        // A Sent row is no longer due, so a second pass never sends it again.
        (await Dispatcher(db, provider, clock).SendOneAsync(email.Id, CancellationToken.None)).Should().BeFalse();
        provider.Sent.Should().HaveCount(1);
        (await EmailDispatcher.Due(db.EmailNotifications, clock.Now).AnyAsync(e => e.Id == email.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData("Rejected", "Your StudyHive booking request was rejected", "rejected your booking request")]
    [InlineData("RevisionRequested", "Your StudyHive booking request needs changes", "asked you to change your booking request")]
    public async Task Reject_And_Revision_Emails_Carry_The_Librarians_Comment(string decision, string subject, string wording)
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        await DecideAsync(proposal.QuotationId, decision, "Please pick an afternoon slot.");

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var email = await QueuedEmailAsync(db, proposal.RequestId);
        var provider = new FakeEmailProvider();

        (await Dispatcher(db, provider, new FakeClock(DateTimeOffset.UtcNow)).SendOneAsync(email.Id, CancellationToken.None)).Should().BeTrue();

        var message = provider.Sent.Should().ContainSingle().Subject;
        message.Subject.Should().Be(subject);
        message.TextBody.Should().Contain(wording).And.Contain("Librarian's comment: Please pick an afternoon slot.");
        (await ReloadAsync(db, email.Id)).Status.Should().Be(EmailNotificationStatus.Sent);
    }

    [Fact]
    public async Task A_Failing_Provider_Backs_Off_Then_Fails_The_Row_After_Three_Attempts()
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        await DecideAsync(proposal.QuotationId, "Approved");

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var email = await QueuedEmailAsync(db, proposal.RequestId);
        var provider = new FakeEmailProvider { FailWith = "Brevo returned 503 Service Unavailable" };
        var t0 = DateTimeOffset.UtcNow;
        var clock = new FakeClock(t0);
        var dispatcher = Dispatcher(db, provider, clock);

        // Attempt 1 fails: still Queued, retry in 60 s.
        (await dispatcher.SendOneAsync(email.Id, CancellationToken.None)).Should().BeTrue();
        var row = await ReloadAsync(db, email.Id);
        row.Status.Should().Be(EmailNotificationStatus.Queued);
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().Be("Brevo returned 503 Service Unavailable");
        row.NextAttemptAt.Should().BeCloseTo(t0.AddSeconds(60), TimeSpan.FromMilliseconds(1));

        // Not due yet: skipped, no attempt counted.
        clock.Now = t0.AddSeconds(59);
        (await EmailDispatcher.Due(db.EmailNotifications, clock.Now).AnyAsync(e => e.Id == email.Id)).Should().BeFalse();
        (await dispatcher.SendOneAsync(email.Id, CancellationToken.None)).Should().BeFalse();
        (await ReloadAsync(db, email.Id)).AttemptCount.Should().Be(1);

        // Attempt 2 fails: the back-off doubles to 120 s.
        clock.Now = t0.AddSeconds(60);
        (await EmailDispatcher.Due(db.EmailNotifications, clock.Now).AnyAsync(e => e.Id == email.Id)).Should().BeTrue();
        (await dispatcher.SendOneAsync(email.Id, CancellationToken.None)).Should().BeTrue();
        row = await ReloadAsync(db, email.Id);
        row.Status.Should().Be(EmailNotificationStatus.Queued);
        row.AttemptCount.Should().Be(2);
        row.NextAttemptAt.Should().BeCloseTo(clock.Now.AddSeconds(120), TimeSpan.FromMilliseconds(1));

        // Attempt 3 reaches max_attempts: Failed, never due again.
        clock.Now = clock.Now.AddSeconds(120);
        (await dispatcher.SendOneAsync(email.Id, CancellationToken.None)).Should().BeTrue();
        row = await ReloadAsync(db, email.Id);
        row.Status.Should().Be(EmailNotificationStatus.Failed);
        row.AttemptCount.Should().Be(3);
        row.NextAttemptAt.Should().BeNull();
        row.SentAt.Should().BeNull();
        row.ProviderMessageId.Should().BeNull();

        clock.Now = clock.Now.AddDays(1);
        (await dispatcher.SendOneAsync(email.Id, CancellationToken.None)).Should().BeFalse();
        provider.Attempts.Should().Be(3);
    }

    [Fact]
    public async Task A_Provider_That_Throws_Counts_As_A_Failed_Attempt()
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        await DecideAsync(proposal.QuotationId, "Approved");

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var email = await QueuedEmailAsync(db, proposal.RequestId);
        var provider = new FakeEmailProvider { ThrowWith = new InvalidOperationException("socket closed") };

        (await Dispatcher(db, provider, new FakeClock(DateTimeOffset.UtcNow)).SendOneAsync(email.Id, CancellationToken.None)).Should().BeTrue();

        var row = await ReloadAsync(db, email.Id);
        row.Status.Should().Be(EmailNotificationStatus.Queued);
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().Be("InvalidOperationException: socket closed");
        row.NextAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_Row_Whose_Booking_Request_Is_Gone_Fails_At_Once_Without_Sending()
    {
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var orphan = new EmailNotification
        {
            Id = Guid.NewGuid(),
            ToEmail = $"orphan-{Guid.NewGuid():N}@example.invalid",
            Template = EmailTemplates.BookingApproved,
            Subject = EmailTemplates.SubjectFor(EmailTemplates.BookingApproved),
            BookingRequestId = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.EmailNotifications.Add(orphan);
        await db.SaveChangesAsync();
        try
        {
            var provider = new FakeEmailProvider();

            (await Dispatcher(db, provider, new FakeClock(DateTimeOffset.UtcNow)).SendOneAsync(orphan.Id, CancellationToken.None)).Should().BeTrue();

            provider.Attempts.Should().Be(0);
            var row = await ReloadAsync(db, orphan.Id);
            row.Status.Should().Be(EmailNotificationStatus.Failed);
            row.LastError.Should().StartWith("Nothing to render");
        }
        finally
        {
            await db.EmailNotifications.Where(e => e.Id == orphan.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public void Without_A_Brevo_Key_Nothing_That_Sends_Is_Registered()
    {
        foreach (var settings in new[]
        {
            new Dictionary<string, string?>(),
            new Dictionary<string, string?> { ["Brevo:ApiKey"] = "", ["Brevo:SenderEmail"] = "sender@example.com" },
            new Dictionary<string, string?> { ["Brevo:ApiKey"] = "key", ["Brevo:SenderEmail"] = " " },
        })
        {
            var services = new ServiceCollection();

            services.AddEmailSender(Config(settings)).Should().BeFalse();

            services.Should().NotContain(d => d.ImplementationType == typeof(EmailSenderService));
            services.Should().NotContain(d => d.ServiceType == typeof(IEmailProvider));
            services.Should().NotContain(d => d.ServiceType == typeof(EmailDispatcher));
        }

        // The real test host (Development, no key) runs no sender, so its Queued rows stay Queued.
        fx.Services.GetServices<IHostedService>().Should().NotContain(s => s is EmailSenderService);
        fx.Services.GetService<IEmailProvider>().Should().BeNull();
    }

    [Fact]
    public void With_A_Key_And_Sender_The_Brevo_Sender_Is_Registered()
    {
        var services = new ServiceCollection();

        services.AddEmailSender(Config(new() { ["Brevo:ApiKey"] = "key", ["Brevo:SenderEmail"] = "sender@example.com" }))
            .Should().BeTrue();

        services.Should().ContainSingle(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(EmailSenderService));
        services.Should().Contain(d => d.ServiceType == typeof(IEmailProvider));
        services.Should().Contain(d => d.ServiceType == typeof(EmailDispatcher));
    }

    [Fact]
    public async Task The_Brevo_Provider_Posts_The_Message_With_The_Api_Key_Header_And_Reads_The_Message_Id()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, """{"messageId":"<202610011200.123@smtp-relay.mailin.fr>"}""");
        var provider = BrevoProvider(handler, apiKey: "xkeysib-test-123");

        var result = await provider.SendAsync(
            new EmailMessage("student@example.com", "Sam Student", "Subject line", "<p>Hi</p>", "Hi"), CancellationToken.None);

        result.Should().Be(EmailSendResult.Sent("<202610011200.123@smtp-relay.mailin.fr>"));
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri("https://api.brevo.com/v3/smtp/email"));
        handler.Request.Headers.GetValues("api-key").Should().Equal("xkeysib-test-123");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("sender").GetProperty("email").GetString().Should().Be("sender@example.com");
        body.RootElement.GetProperty("sender").GetProperty("name").GetString().Should().Be("StudyHive");
        body.RootElement.GetProperty("to")[0].GetProperty("email").GetString().Should().Be("student@example.com");
        body.RootElement.GetProperty("subject").GetString().Should().Be("Subject line");
        body.RootElement.GetProperty("htmlContent").GetString().Should().Be("<p>Hi</p>");
        body.RootElement.GetProperty("textContent").GetString().Should().Be("Hi");
    }

    [Fact]
    public async Task A_Brevo_Error_Becomes_A_Failed_Result_Without_The_Key()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, """{"code":"unauthorized","message":"Key not found"}""");
        var provider = BrevoProvider(handler, apiKey: "xkeysib-secret-999");

        var result = await provider.SendAsync(new EmailMessage("s@example.com", "S", "Subj", "<p>x</p>", "x"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Brevo returned 401").And.Contain("Key not found").And.NotContain("xkeysib-secret-999");
    }

    private async Task DecideAsync(Guid quotationId, string decision, string? comments = null)
    {
        var response = await fx.Client(fx.LibrarianToken).PostAsJsonAsync("/api/approvals", new { quotationId, decision, comments });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<EmailNotification> QueuedEmailAsync(StudyHiveDbContext db, Guid requestId)
    {
        var email = await db.EmailNotifications.AsNoTracking().SingleAsync(e => e.BookingRequestId == requestId);
        email.Status.Should().Be(EmailNotificationStatus.Queued);
        return email;
    }

    private static Task<EmailNotification> ReloadAsync(StudyHiveDbContext db, Guid emailId) =>
        db.EmailNotifications.AsNoTracking().SingleAsync(e => e.Id == emailId);

    private static EmailDispatcher Dispatcher(StudyHiveDbContext db, IEmailProvider provider, TimeProvider clock) =>
        new(db, new EmailRenderer(db), provider,
            Options.Create(new BrevoOptions { ApiKey = "unused", SenderEmail = "sender@example.com", RetryBaseSeconds = 60 }),
            clock, NullLogger<EmailDispatcher>.Instance);

    private static IConfiguration Config(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    /// <summary>The provider exactly as the app registers it (base address and api-key header),
    /// with the network replaced by <paramref name="handler"/>.</summary>
    private static IEmailProvider BrevoProvider(HttpMessageHandler handler, string apiKey)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmailSender(Config(new() { ["Brevo:ApiKey"] = apiKey, ["Brevo:SenderEmail"] = "sender@example.com" }));
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler));
        return services.BuildServiceProvider().GetRequiredService<IEmailProvider>();
    }
}

internal sealed class FakeEmailProvider : IEmailProvider
{
    public List<EmailMessage> Sent { get; } = [];
    public int Attempts { get; private set; }
    public string? FailWith { get; init; }
    public Exception? ThrowWith { get; init; }

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        Attempts++;
        if (ThrowWith is not null) throw ThrowWith;
        if (FailWith is not null) return Task.FromResult(EmailSendResult.Failed(FailWith));
        Sent.Add(message);
        return Task.FromResult(EmailSendResult.Sent($"fake-{Sent.Count}"));
    }
}

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class RecordingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
    }
}
