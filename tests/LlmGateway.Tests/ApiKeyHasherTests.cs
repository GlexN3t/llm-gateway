using LlmGateway.Core.Security;

namespace LlmGateway.Tests;

public class ApiKeyHasherTests
{
    [Fact]
    public void Verifies_matching_key()
    {
        var hasher = new ApiKeyHasher();
        var (hash, salt) = hasher.Hash("gw-secret");

        Assert.True(hasher.Verify("gw-secret", hash, salt));
    }

    [Fact]
    public void Rejects_wrong_key()
    {
        var hasher = new ApiKeyHasher();
        var (hash, salt) = hasher.Hash("gw-secret");

        Assert.False(hasher.Verify("gw-other", hash, salt));
    }

    [Fact]
    public void Fresh_salt_produces_different_hash()
    {
        var hasher = new ApiKeyHasher();
        var first = hasher.Hash("same-key");
        var second = hasher.Hash("same-key");

        Assert.NotEqual(first.Hash, second.Hash);
        Assert.NotEqual(first.Salt, second.Salt);
    }
}
