using Xunit;

public class DbFactoryTests
{
    [Fact]
    public void CanCreateDbContext()
    {
        using var db = TestDbContextFactory.Create();
        Assert.NotNull(db);
    }
}