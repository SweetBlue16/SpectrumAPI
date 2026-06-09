using Spectrum.API.Dtos.Admin;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Services.Admin;
using Spectrum.API.Utilities;
using Spectrum.Tests.Helpers;

namespace Spectrum.Tests.UnitTests.Services
{
    public class AdminProfileServiceTests
    {
        [Fact]
        public async Task GetProfileAsyncWhenAdminExistsShouldMapDetail()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var admin = CreateAdmin();
            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
            var service = new AdminProfileService(context);

            var result = await service.GetProfileAsync(admin.Id);

            Assert.Equal(admin.Id, result.Id);
            Assert.Equal("admin", result.Username);
            Assert.Equal("Ana", result.FirstName);
            Assert.Equal(Constants.Roles.Admin, result.Role);
        }

        [Fact]
        public async Task UpdateProfileAsyncShouldTrimAndPersistProfileFields()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var admin = CreateAdmin();
            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
            var service = new AdminProfileService(context);

            var result = await service.UpdateProfileAsync(admin.Id, new UpdateAdminProfileDto
            {
                Username = " updated ",
                FirstName = " Maria ",
                LastName = " Lopez ",
                PhoneNumber = " +52551111 ",
                Address = " New address ",
                ProfilePicture = " https://cdn.test/admin.png "
            });

            Assert.Equal("updated", result.Username);
            Assert.Equal("Maria", admin.AdminDetail!.FirstName);
            Assert.Equal("+52551111", admin.AdminDetail.PhoneNumber);
            Assert.Equal("https://cdn.test/admin.png", admin.ProfilePicture);
        }

        [Fact]
        public async Task UpdateProfileAsyncWhenUsernameIsTakenShouldThrow()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var admin = CreateAdmin();
            await context.Users.AddRangeAsync(admin, new User
            {
                Id = Guid.NewGuid(),
                Username = "taken",
                Email = "taken@test.com",
                PasswordHash = "hash",
                Role = Constants.Roles.Reviewer
            });
            await context.SaveChangesAsync();
            var service = new AdminProfileService(context);

            await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                service.UpdateProfileAsync(admin.Id, new UpdateAdminProfileDto
                {
                    Username = "taken",
                    FirstName = "Maria",
                    LastName = "Lopez",
                    PhoneNumber = "+52551111",
                    Address = "New address"
                }));
        }

        [Fact]
        public async Task GetProfileAsyncWhenAdminDetailIsMissingShouldThrow()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await context.Users.AddAsync(new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@test.com",
                PasswordHash = "hash",
                Role = Constants.Roles.Admin
            });
            await context.SaveChangesAsync();
            var service = new AdminProfileService(context);

            await Assert.ThrowsAsync<SpectrumNotFoundException>(() => service.GetProfileAsync(Guid.NewGuid()));
        }

        private static User CreateAdmin()
        {
            var adminId = Guid.NewGuid();
            return new User
            {
                Id = adminId,
                Username = "admin",
                Email = "admin@test.com",
                PasswordHash = "hash",
                Role = Constants.Roles.Admin,
                ProfilePicture = "old.png",
                AdminDetail = new AdminDetail
                {
                    Id = Guid.NewGuid(),
                    UserId = adminId,
                    FirstName = "Ana",
                    LastName = "Admin",
                    PhoneNumber = "+52550000",
                    Address = "Old address",
                    Rfc = "ABCD123456EFG"
                }
            };
        }
    }
}
