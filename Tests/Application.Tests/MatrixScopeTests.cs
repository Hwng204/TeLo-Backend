using Domain.Entities.QuestionBank;
using Xunit;

namespace Application.Tests;

public sealed class MatrixScopeTests
{
    [Fact]
    public void Key_RoundTrips()
    {
        var scope = new MatrixScope(12, 3, 999);
        Assert.Equal(12_003_999UL, scope.Key);
        Assert.True(MatrixScope.TryParse(scope.Key, out var parsed));
        Assert.Equal(scope, parsed);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(999UL)] // no branch
    [InlineData(1_000_005UL)] // no subject
    [InlineData(1_003_000UL)] // no grade
    public void TryParse_RejectsKeysWithAMissingPart(ulong key)
    {
        Assert.False(MatrixScope.TryParse(key, out _));
    }

    [Fact]
    public void Key_RefusesIdsThatWouldCollide()
    {
        Assert.Throws<InvalidOperationException>(() => new MatrixScope(1, 1000, 1).Key);
    }
}
