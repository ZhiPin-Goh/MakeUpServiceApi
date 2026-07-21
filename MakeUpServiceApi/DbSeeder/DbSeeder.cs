using MakeUpServiceApi.DTO.AdminDTO;
using MakeUpServiceApi.Function;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using Scrypt;

namespace MakeUpServiceApi.DbSeeder
{
    public class DbSeeder
    {
        private static ScryptEncoder encoder = new ScryptEncoder();
        public static async Task SeedAdminsAsync(IServiceProvider serviceProvider)
        {
            using var context = serviceProvider.GetRequiredService<AppDbContext>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            var admninAccount = config.GetSection("AdminAccount").Get<List<AdminConfigDto>>();

            if (admninAccount != null)
            {
                foreach (var adminConfig in admninAccount)
                {
                    var exists = await context.Admins.AnyAsync(a => a.UserName == adminConfig.UserName);
                    if (!exists)
                    {
                        var newAdmin = new Admin
                        {
                            UserName = adminConfig.UserName,
                            PasswordHash = encoder.Encode(adminConfig.PasswordHash),
                            Email = adminConfig.Email,
                            CreatedAt = DateTime.Now
                        };
                        context.Admins.Add(newAdmin);
                    }
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
