using ObjectDetection.Domain;

namespace ObjectDetection.Tests;

public class ApiKeyHasherTests
{
    private const string KnownKey = "dev-key-001";
    private const string KnownHash = "53ee0b20282328ce6a0ba8edc5940bc75eb3307f4f74408b703ec0d03f75eb7a";

    [Fact]
    public void Hash_是固定的64位小写十六进制()
    {
        var hash = ApiKeyHasher.Hash(KnownKey);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        // 与数据库迁移里 HASHBYTES 的结果一致（已验证）
        Assert.Equal(KnownHash, hash);
    }

    [Fact]
    public void Verify_正确key通过()
        => Assert.True(ApiKeyHasher.Verify(KnownKey, ApiKeyHasher.Hash(KnownKey)));

    [Fact]
    public void Verify_错误key拒绝()
        => Assert.False(ApiKeyHasher.Verify("dev-key-002", KnownHash));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_明文为空拒绝(string? plain)
        => Assert.False(ApiKeyHasher.Verify(plain, KnownHash));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_哈希为空拒绝(string? hash)
        => Assert.False(ApiKeyHasher.Verify(KnownKey, hash));
}
