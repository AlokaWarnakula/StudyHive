using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using StudyHive.Api.Controllers.Store;

namespace StudyHive.Api.Tests;

public class SuppliersControllerTests(S3Fixture s3) : IClassFixture<S3Fixture>
{
    private readonly S3Fixture _s3 = s3;

    private static object NewSupplier(string? name = null, string email = "orders@supplier.test") => new
    {
        name = name ?? S3Fixture.UniqueName("Supplier"),
        contactEmail = email,
        phone = "011 234 5678",
        address = "12 Galle Road, Colombo",
    };

    private async Task<SupplierResponse> CreateAsync(object body)
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/suppliers", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<SupplierResponse>(TestSupport.JsonOptions))!;
        _s3.SupplierIds.Add(created.Id);
        return created;
    }

    [Fact]
    public async Task Anonymous_Request_Returns_401()
    {
        var response = await _s3.Client(null).GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Student_Cannot_Create_Or_List_Suppliers()
    {
        var client = _s3.Client(_s3.StudentToken);

        (await client.PostAsJsonAsync("/api/suppliers", NewSupplier())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/suppliers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Store_Officer_Can_Create_List_And_Update_A_Supplier()
    {
        var created = await CreateAsync(NewSupplier());
        created.IsActive.Should().BeTrue();

        var client = _s3.Client(_s3.StoreOfficerToken);
        var list = await client.GetFromJsonAsync<PagedResultShape<SupplierResponse>>(
            $"/api/suppliers?search={Uri.EscapeDataString(created.Name)}", TestSupport.JsonOptions);
        list!.Items.Should().ContainSingle(s => s.Id == created.Id);

        var update = await client.PutAsJsonAsync($"/api/suppliers/{created.Id}", new
        {
            name = created.Name,
            contactEmail = "sales@supplier.test",
            phone = "011 999 0000",
            address = (string?)null,
            isActive = false,
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<SupplierResponse>(TestSupport.JsonOptions))!;
        updated.ContactEmail.Should().Be("sales@supplier.test");
        updated.IsActive.Should().BeFalse();

        var activeList = await client.GetFromJsonAsync<PagedResultShape<SupplierResponse>>(
            $"/api/suppliers?search={Uri.EscapeDataString(created.Name)}", TestSupport.JsonOptions);
        activeList!.Items.Should().BeEmpty();
        var allList = await client.GetFromJsonAsync<PagedResultShape<SupplierResponse>>(
            $"/api/suppliers?activeOnly=false&search={Uri.EscapeDataString(created.Name)}", TestSupport.JsonOptions);
        allList!.Items.Should().ContainSingle(s => s.Id == created.Id);
    }

    [Fact]
    public async Task Invalid_Email_Returns_400()
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/suppliers", NewSupplier(email: "not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_Name_Returns_409()
    {
        var created = await CreateAsync(NewSupplier());

        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/suppliers", NewSupplier(created.Name));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Updating_An_Unknown_Supplier_Returns_404()
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).PutAsJsonAsync($"/api/suppliers/{Guid.NewGuid()}", new
        {
            name = S3Fixture.UniqueName("Ghost"),
            contactEmail = "ghost@supplier.test",
            phone = "011 000 0000",
            isActive = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
