using RestaurantSystem.Services;

namespace RestaurantSystem.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hash = PasswordHasher.Hash("секрет123");
        Assert.True(PasswordHasher.Verify("секрет123", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hash = PasswordHasher.Hash("секрет123");
        Assert.False(PasswordHasher.Verify("секрет124", hash));
    }

    [Fact]
    public void Hash_UsesRandomSalt()
    {
        Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("1.@@@.###")]
    public void Verify_ReturnsFalse_ForMalformedHash(string stored)
    {
        Assert.False(PasswordHasher.Verify("anything", stored));
    }
}
