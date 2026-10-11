using FinSight.Infrastructure.Persistence;
using Npgsql;

namespace FinSight.UnitTests.Website;

public class NeonMigrationConnectionTests
{
    [Fact]
    public void DirectUrl_DecodesCredentialsOnceAndQuotesNpgsqlValues()
    {
        var parsed = new NpgsqlConnectionStringBuilder(NeonMigrationConnection.FromDirectUrl(
            "postgresql://owner%40example:p%3Ba%3D%22ss%2B%2525@ep-test.region.aws.neon.tech/pilot%20db?sslmode=require&channel_binding=require"));
        Assert.Equal("owner@example", parsed.Username);
        Assert.Equal("p;a=\"ss+%25", parsed.Password);
        Assert.Equal("pilot db", parsed.Database);
        Assert.Equal(SslMode.VerifyFull, parsed.SslMode);
        Assert.Equal(ChannelBinding.Require, parsed.ChannelBinding);
        Assert.Equal(GssEncryptionMode.Disable, parsed.GssEncryptionMode);
        Assert.False(parsed.Pooling);
    }

    [Fact]
    public void LiteralPlusAndColonInPassword_ArePreserved()
    {
        var parsed = new NpgsqlConnectionStringBuilder(NeonMigrationConnection.FromDirectUrl(
            "postgres://owner:p+a:ss@ep-test.region.aws.neon.tech/pilot"));
        Assert.Equal("p+a:ss", parsed.Password);
        Assert.Equal(SslMode.VerifyFull, parsed.SslMode);
        Assert.Equal(ChannelBinding.Require, parsed.ChannelBinding);
        Assert.Equal(GssEncryptionMode.Disable, parsed.GssEncryptionMode);
    }

    [Theory]
    [InlineData("postgresql://owner:secret@ep-test-pooler.region.aws.neon.tech/pilot")]
    [InlineData("postgresql://owner:secret@localhost/pilot")]
    [InlineData("postgresql://owner:secret@ep-test.neon.tech.evil.example/pilot")]
    [InlineData("postgresql://owner:secret@ep-test.neon.tech/pilot?sslmode=disable")]
    [InlineData("postgresql://owner:secret@ep-test.neon.tech/pilot?options=search_path%3Devil")]
    [InlineData("postgresql://owner:secret@ep-test.neon.tech/pilot?sslmode=require&sslmode=verify-full")]
    [InlineData("postgresql://owner:secret%ZZ@ep-test.neon.tech/pilot")]
    [InlineData("postgresql://owner:secret@ep-test.neon.tech/pilot#fragment")]
    public void UnsafeOrPooledUrl_IsRejectedWithoutEchoingCredentials(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => NeonMigrationConnection.FromDirectUrl(value));
        Assert.DoesNotContain("secret", error.ToString());
    }
}
