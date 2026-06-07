using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;

namespace Spectrum.Tests.Helpers
{
    internal static class TestDbContextFactory
    {
        public static SpectrumDbContext CreateContext(string? databaseName = null)
        {
            var options = new DbContextOptionsBuilder<SpectrumDbContext>()
                .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
                .Options;

            return new SpectrumDbContext(options);
        }
    }
}
