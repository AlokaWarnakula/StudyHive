using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

public class StudentProfilesControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());

    [Fact]
    public async Task Student_Can_Create_Their_Own_Profile()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/student-profiles", new
        {
            studentNumber = $"S{Guid.NewGuid():N}"[..12],
            department = "Computing",
            yearOfStudy = 2,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<StudentProfileResponseShape>(TestSupport.JsonOptions);
        body!.UserId.Should().Be(user.Id);
        body.MaxBookingsPerWeek.Should().Be(3); // entity default
        body.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Creating_A_Second_Profile_For_The_Same_Account_Returns_409()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);

        var response = await client.PostAsJsonAsync("/api/student-profiles", new
        {
            studentNumber = $"S{Guid.NewGuid():N}"[..12],
            department = "Computing",
            yearOfStudy = 2,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Librarian_Cannot_Create_A_Student_Profile()
    {
        var client = factory.CreateClient();
        var (userId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(userId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/student-profiles", new
        {
            studentNumber = $"S{Guid.NewGuid():N}"[..12],
            department = "Computing",
            yearOfStudy = 2,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Own_Profile_Returns_404_Before_Onboarding_And_200_After()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        (await client.GetAsync("/api/student-profiles/me")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await TestSupport.CreateStudentProfileAsync(client, token);

        var response = await client.GetAsync("/api/student-profiles/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Student_Cannot_View_Another_Students_Profile()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var ownerProfile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (other, _, otherToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(other.Id);

        client.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);
        var response = await client.GetAsync($"/api/student-profiles/{ownerProfile.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Librarian_Can_List_And_View_Any_Student_Profile()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", librarianToken);

        var getResponse = await client.GetAsync($"/api/student-profiles/{profile.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync("/api/student-profiles?pageSize=100");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_Admin_Can_Update_A_Student_Profile()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", librarianToken);

        var forbidden = await client.PutAsJsonAsync($"/api/student-profiles/{profile.Id}", new
        {
            department = "Engineering",
            yearOfStudy = 3,
            maxBookingsPerWeek = 5,
            penaltyPoints = 0,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        var updateResponse = await client.PutAsJsonAsync($"/api/student-profiles/{profile.Id}", new
        {
            department = "Engineering",
            yearOfStudy = 3,
            maxBookingsPerWeek = 5,
            penaltyPoints = 2,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<StudentProfileResponseShape>(TestSupport.JsonOptions);
        updated!.Department.Should().Be("Engineering");
        updated.MaxBookingsPerWeek.Should().Be(5);
        updated.PenaltyPoints.Should().Be(2);
    }

    [Fact]
    public async Task Fresh_Profile_Is_Eligible_With_No_Reasons()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        var response = await client.GetAsync($"/api/student-profiles/{profile.Id}/eligibility");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EligibilityResponseShape>(TestSupport.JsonOptions);
        body!.Eligible.Should().BeTrue();
        body.Reasons.Should().BeEmpty();
    }

    /// <summary>DOCS Master Plan draws the line at 3: "hold fewer than 3 penalty points". One or two
    /// points leave a student eligible, which is why this asserts on 3 rather than on any point at all.</summary>
    [Fact]
    public async Task Three_Penalty_Points_Make_A_Student_Ineligible()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/student-profiles/{profile.Id}", new
        {
            department = profile.Department,
            yearOfStudy = profile.YearOfStudy,
            maxBookingsPerWeek = profile.MaxBookingsPerWeek,
            penaltyPoints = 3,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });

        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
        var response = await client.GetAsync($"/api/student-profiles/{profile.Id}/eligibility");

        var body = await response.Content.ReadFromJsonAsync<EligibilityResponseShape>(TestSupport.JsonOptions);
        body!.Eligible.Should().BeFalse();
        body.Reasons.Should().Contain(r => r.Contains("penalty", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The boundary itself: two points is under the plan's limit, so eligibility must still
    /// come back true with no penalty reason attached.</summary>
    [Fact]
    public async Task Two_Penalty_Points_Leave_A_Student_Eligible()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/student-profiles/{profile.Id}", new
        {
            department = profile.Department,
            yearOfStudy = profile.YearOfStudy,
            maxBookingsPerWeek = profile.MaxBookingsPerWeek,
            penaltyPoints = 2,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });

        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
        var response = await client.GetAsync($"/api/student-profiles/{profile.Id}/eligibility");

        var body = await response.Content.ReadFromJsonAsync<EligibilityResponseShape>(TestSupport.JsonOptions);
        body!.Eligible.Should().BeTrue();
        body.Reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task StoreOfficer_Cannot_View_A_Students_Profile_Or_Eligibility()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (storeOfficerId, _, storeOfficerToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.StoreOfficer);
        _createdUserIds.Add(storeOfficerId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", storeOfficerToken);

        (await client.GetAsync($"/api/student-profiles/{profile.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/student-profiles/{profile.Id}/eligibility")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Cannot_Read_A_Students_Profile_Or_Eligibility()
    {
        var client = factory.CreateClient();
        var (owner, _, ownerToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(owner.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, ownerToken);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        (await client.GetAsync($"/api/student-profiles/{profile.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/student-profiles/{profile.Id}/eligibility")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StoreOfficer_Cannot_List_Student_Profiles()
    {
        var client = factory.CreateClient();
        var (storeOfficerId, _, storeOfficerToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.StoreOfficer);
        _createdUserIds.Add(storeOfficerId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", storeOfficerToken);

        var response = await client.GetAsync("/api/student-profiles?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

internal sealed class EligibilityResponseShape
{
    public bool Eligible { get; init; }
    public List<string> Reasons { get; init; } = [];
}
