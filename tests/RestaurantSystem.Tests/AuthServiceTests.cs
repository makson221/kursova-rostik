using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void Login_WithValidCredentials_SetsCurrentUser()
    {
        var auth = new AuthService(_db.Factory);
        var user = auth.Login("admin", "admin");

        Assert.Equal(UserRole.Administrator, user.Role);
        Assert.Same(user, auth.CurrentUser);
        Assert.True(auth.IsInRole(UserRole.Administrator));
    }

    [Fact]
    public void Login_IsCaseInsensitiveForLogin()
    {
        var auth = new AuthService(_db.Factory);
        Assert.Equal("waiter", auth.Login("  WAITER ", "1111").Login);
    }

    [Fact]
    public void Login_WithWrongPassword_Throws()
    {
        var auth = new AuthService(_db.Factory);
        var ex = Assert.Throws<BusinessException>(() => auth.Login("admin", "wrong"));
        Assert.Contains("Невірний", ex.Message);
        Assert.Null(auth.CurrentUser);
    }

    [Fact]
    public void Login_BlockedEmployee_Throws()
    {
        var staff = new StaffService(_db.Factory);
        var admin = _db.User("admin");
        var cook = _db.User("maria");
        cook.IsActive = false;
        staff.Save(cook, null, admin.Id);

        var auth = new AuthService(_db.Factory);
        var ex = Assert.Throws<BusinessException>(() => auth.Login("maria", "2222"));
        Assert.Contains("заблоковано", ex.Message);
    }

    [Fact]
    public void Logout_ClearsUser_AndRaisesEvent()
    {
        var auth = new AuthService(_db.Factory);
        auth.Login("cook", "2222");
        var raised = false;
        auth.CurrentUserChanged += (_, _) => raised = true;

        auth.Logout();

        Assert.Null(auth.CurrentUser);
        Assert.True(raised);
    }

    public void Dispose() => _db.Dispose();
}
