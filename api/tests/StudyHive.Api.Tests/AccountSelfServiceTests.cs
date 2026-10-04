using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Auth;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

/// <summary>PLAN.md 3.1: student edits own profile, any user changes own password, Librarian
/// records a manual desk payment.</summary>
public class AccountSelfServiceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());

    private static object ProfileBody(string studentNumber, string fullName = "Edited Name") => new
    {
        fullName,
        studentNumber,
        department = "Engineering",
        yearOfStudy = 3,
    };

    private static string NewNumber() => $"S{Guid.NewGuid():N}"[..12];

    // ---- a) PUT /api/student-profiles/me ----

    [Fact]
    public async Task Student_Edits_Own_Profile_And_Limits_Stay_Unchanged()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        var before = await TestSupport.CreateStudentProfileAsync(client, token);
        var number = NewNumber();

        var response = await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody(number));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("fullName").GetString().Should().Be("Edited Name");
        body.GetProperty("studentNumber").GetString().Should().Be(number);
        body.GetProperty("department").GetString().Should().Be("Engineering");
        body.GetProperty("yearOfStudy").GetInt32().Should().Be(3);
        body.GetProperty("maxBookingsPerWeek").GetInt32().Should().Be(before.MaxBookingsPerWeek);
        body.GetProperty("penaltyPoints").GetInt32().Should().Be(before.PenaltyPoints);

        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me", TestSupport.JsonOptions);
        me!.FullName.Should().Be("Edited Name");
    }

    [Fact]
    public async Task Student_Number_Taken_By_Another_Student_Returns_409()
    {
        var client = factory.CreateClient();
        var (other, _, otherToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(other.Id);
        var taken = (await TestSupport.CreateStudentProfileAsync(client, otherToken)).StudentNumber;

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);

        var response = await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody(taken));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Keeping_Own_Student_Number_Is_Not_A_Conflict()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, token);

        var response = await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody(profile.StudentNumber));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Profile_Validates_And_Needs_A_Profile()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        (await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody(NewNumber())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await TestSupport.CreateStudentProfileAsync(client, token);
        (await client.PutAsJsonAsync("/api/student-profiles/me", new { fullName = "X", studentNumber = NewNumber(), department = "Eng", yearOfStudy = 9 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody("   ")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Edit_Profile_Is_Student_Only()
    {
        var client = factory.CreateClient();
        (await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody(NewNumber())))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (librarianId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        (await client.PutAsJsonAsync("/api/student-profiles/me", ProfileBody(NewNumber())))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- b) POST /api/auth/change-password ----

    [Fact]
    public async Task Change_Password_Swaps_Passwords_And_Signs_Out_Other_Sessions()
    {
        var client = factory.CreateClient();
        var email = TestSupport.UniqueEmail("student");
        var user = await TestSupport.RegisterStudentAsync(client, email);
        _createdUserIds.Add(user.Id);
        var otherDevice = await TestSupport.LoginAsync(client, email);
        var thisDevice = await TestSupport.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", thisDevice.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestSupport.Password, newPassword = "A-Brand-New-Password-2" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fresh = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(TestSupport.JsonOptions);
        fresh!.RefreshToken.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization = null;
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSupport.Password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "A-Brand-New-Password-2" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = otherDevice.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = fresh.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Change_Password_Rejects_Wrong_Current_Short_Or_Same_Password()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var wrong = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "not-my-password", newPassword = "A-Brand-New-Password-2" });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadAsStringAsync()).Should().Contain("CurrentPassword");

        (await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestSupport.Password, newPassword = "short" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestSupport.Password, newPassword = TestSupport.Password }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Change_Password_Works_For_Staff_And_Requires_Sign_In()
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestSupport.Password, newPassword = "A-Brand-New-Password-2" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (adminId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        (await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestSupport.Password, newPassword = "A-Brand-New-Password-2" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- c) POST /api/booking-requests/{id}/payment ----

    [Fact]
    public async Task Librarian_Marks_Approved_Request_Paid_Once_And_It_Shows_Everywhere()
    {
        var (requestId, quotationId, studentToken, client) = await SeedRequestAsync(approved: true);
        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", librarianToken);

        var response = await client.PostAsJsonAsync($"/api/booking-requests/{requestId}/payment", new { paymentReference = " RCPT-0042 " });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.String);
        body.GetProperty("paymentReference").GetString().Should().Be("RCPT-0042");

        (await client.PostAsJsonAsync($"/api/booking-requests/{requestId}/payment", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var quotation = await client.GetFromJsonAsync<JsonElement>($"/api/quotations/{quotationId}");
        quotation.GetProperty("paymentReference").GetString().Should().Be("RCPT-0042");

        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        var request = await client.GetFromJsonAsync<JsonElement>($"/api/booking-requests/{requestId}");
        request.GetProperty("latestQuotation").GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.String);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.Quotations.SingleAsync(q => q.Id == quotationId)).PaidBy.Should().Be(librarianId);
        (await db.AuditLogs.AnyAsync(a => a.EntityId == quotationId && a.Action == "PaymentRecorded")).Should().BeTrue();
    }

    [Fact]
    public async Task Payment_On_Unapproved_Or_Unknown_Request_Is_Refused()
    {
        var (requestId, _, _, client) = await SeedRequestAsync(approved: false);
        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", librarianToken);

        (await client.PostAsJsonAsync($"/api/booking-requests/{requestId}/payment", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync($"/api/booking-requests/{Guid.NewGuid()}/payment", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(UserRole.StoreOfficer)]
    [InlineData(UserRole.Admin)]
    public async Task Only_The_Librarian_Records_Payment(UserRole role)
    {
        var (requestId, _, studentToken, client) = await SeedRequestAsync(approved: true);

        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        (await client.PostAsJsonAsync($"/api/booking-requests/{requestId}/payment", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (staffId, _, staffToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, role);
        _createdUserIds.Add(staffId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", staffToken);
        (await client.PostAsJsonAsync($"/api/booking-requests/{requestId}/payment", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A student with a Draft request, then (direct to the database) either an Approved
    /// request with an Approved quotation, or a PendingApproval one with a Proposed quotation.</summary>
    private async Task<(Guid RequestId, Guid QuotationId, string StudentToken, HttpClient Client)> SeedRequestAsync(bool approved)
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);

        var created = await client.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "Group study session for a database systems assignment",
            groupSize = 4,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 120,
            budget = 50m,
            items = Array.Empty<object>(),
        });
        created.EnsureSuccessStatusCode();
        var requestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var request = await db.BookingRequests.SingleAsync(r => r.Id == requestId);
        request.Status = approved ? BookingRequestStatus.Approved : BookingRequestStatus.PendingApproval;
        var now = DateTimeOffset.UtcNow;
        var quotation = new Quotation
        {
            Id = Guid.NewGuid(),
            BookingRequestId = requestId,
            RoomFee = 30m,
            ConsumableCost = 5m,
            BudgetSnapshot = 50m,
            Status = approved ? QuotationStatus.Approved : QuotationStatus.Proposed,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Quotations.Add(quotation);
        await db.SaveChangesAsync();

        return (requestId, quotation.Id, token, client);
    }
}
