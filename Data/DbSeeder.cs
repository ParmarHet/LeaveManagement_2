using Microsoft.AspNetCore.Identity;
using LMS.Models;
using LMS.Constants;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var roles = new[] { Roles.Admin, Roles.Manager, Roles.Employee };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["EmailSettings:AdminEmail"] ?? "admin@lms.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        
        var adminPassword = configuration["EmailSettings:AdminPassword"] ?? "Admin@123";
        
        if (adminUser == null)
        {
            var newAdmin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Admin",
                DateJoined = DateTime.UtcNow,
                IsActive = true,
                Status = UserStatus.Active
            };
            
            var result = await userManager.CreateAsync(newAdmin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, Roles.Admin);
            }
        }
        else
        {
            // Ensure existing admin is always active and has the Admin role
            bool needsUpdate = false;
            if (!adminUser.IsActive) { adminUser.IsActive = true; needsUpdate = true; }
            if (adminUser.Status != UserStatus.Active) { adminUser.Status = UserStatus.Active; needsUpdate = true; }
            if (needsUpdate) await userManager.UpdateAsync(adminUser);

            var adminRoles = await userManager.GetRolesAsync(adminUser);
            if (!adminRoles.Contains(Roles.Admin))
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }
    }

    public static async Task SeedLeaveTypesAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        
        var types = new List<LeaveType>
        {
            new() { Name = "Weekly Off", Code = "WO", DefaultDays = 0, YearlyLimit = 0, IsPaid = true, RequiresApproval = false, IsEnabled = true },
            new() { Name = "Holidays", Code = "HD", DefaultDays = 10, YearlyLimit = 10, IsPaid = true, RequiresApproval = false, IsEnabled = true },
            new() { Name = "Floating Holiday", Code = "FD", DefaultDays = 4, YearlyLimit = 4, IsPaid = true, RequiresApproval = true, IsEnabled = true },
            new() { Name = "Paid Leave", Code = "PL", DefaultDays = 18, YearlyLimit = 18, IsPaid = true, RequiresApproval = true, CarryForward = true, MaxConsecutiveDays = 0, IsEnabled = true },
            new() { Name = "Sick Leave", Code = "SL", DefaultDays = 7, YearlyLimit = 7, IsPaid = true, RequiresApproval = true, IsEnabled = true },
            new() { Name = "Casual Leave", Code = "CL", DefaultDays = 7, YearlyLimit = 7, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 2, IsEnabled = true },
            new() { Name = "Compensatory Off", Code = "CO", DefaultDays = 0, YearlyLimit = 0, IsPaid = true, RequiresApproval = true, IsEnabled = true },
            new() { Name = "Leave without Pay", Code = "LW", DefaultDays = 0, YearlyLimit = 0, IsPaid = false, RequiresApproval = true, IsEnabled = true },
            new() { Name = "Maternity Leave", Code = "ML", DefaultDays = 0, YearlyLimit = 0, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 182, IsEnabled = true },
            new() { Name = "Bereavement Leave", Code = "BL", DefaultDays = 0, YearlyLimit = 0, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 5, IsEnabled = true }
        };

        foreach (var type in types)
        {
            var existing = await context.LeaveTypes.FirstOrDefaultAsync(lt => lt.Code == type.Code);
            if (existing == null)
            {
                type.DateCreated = DateTime.UtcNow;
                type.DateModified = DateTime.UtcNow;
                context.LeaveTypes.Add(type);
            }
            else
            {
                // Sync YearlyLimit and MaxConsecutiveDays if they are not set properly
                bool needsUpdate = false;
                
                // For ML and BL specifically, update DefaultDays and YearlyLimit to 0 if requested
                if ((type.Code == "ML" || type.Code == "BL") && existing.DefaultDays > 0)
                {
                    existing.DefaultDays = 0;
                    existing.YearlyLimit = 0;
                    needsUpdate = true;
                }

                if (existing.YearlyLimit == 0 && type.YearlyLimit > 0) { existing.YearlyLimit = type.YearlyLimit; needsUpdate = true; }
                if (existing.MaxConsecutiveDays == 0 && type.MaxConsecutiveDays > 0) { existing.MaxConsecutiveDays = type.MaxConsecutiveDays; needsUpdate = true; }
                
                if (needsUpdate)
                {
                    existing.DateModified = DateTime.UtcNow;
                    context.LeaveTypes.Update(existing);
                }
            }
        }
        await context.SaveChangesAsync();

        // ** Auto-allocate default days to all active users for standard paid leaves **
        // ** Auto-allocate default days to all active users for standard paid leaves **
        var allocationService = serviceProvider.GetRequiredService<LMS.Services.ILeaveAllocationService>();
        await allocationService.AllocateDefaultLeavesToAllActiveUsersAsync(DateTime.Now.Year);
    }
}
