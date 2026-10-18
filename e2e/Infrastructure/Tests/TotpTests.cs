using NUnit.Framework;

namespace e2e.Infrastructure.Tests;

[TestFixture]
[Category("Infrastructure")]
public sealed class TotpTests
{
    [TestCase(59L, "287082")]
    [TestCase(1111111109L, "081804")]
    [TestCase(1234567890L, "005924")]
    public void SixDigitCodesMatchRfc6238Vectors(long seconds, string expected)
    {
        //Arrange
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

        //Act
        var code = Totp.Code(secret, seconds / 30);

        //Assert
        Assert.That(code, Is.EqualTo(expected));
    }
}
