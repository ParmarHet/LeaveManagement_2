using Microsoft.EntityFrameworkCore;
using LMS.Models;
using LMS.Data;

namespace LMS.Services;

public class LeaveAllocationService(ApplicationDbContext context) : ILeaveAllocationService
{
    private readonly ApplicationDbContext _context = context;

    public async Task AllocateDefaultLeavesAsync(string userId, int year)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return;

        var leaveTypes = await _context.LeaveTypes
            .Where(t => t.DefaultDays > 0 && t.IsEnabled)
            .ToListAsync();

        foreach (var type in leaveTypes)
        {
            var hasAllocation = await _context.LeaveAllocations
                .AnyAsync(a => a.EmployeeId == userId && a.LeaveTypeId == type.Id && a.Period == year);

            if (!hasAllocation)
            {
                _context.LeaveAllocations.Add(new LeaveAllocation
                {
                    EmployeeId = userId,
                    LeaveTypeId = type.Id,
                    NumberOfDays = type.DefaultDays,
                    Period = year,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task AllocateDefaultLeavesToAllActiveUsersAsync(int year)
    {
        var leaveTypes = await _context.LeaveTypes
            .Where(t => t.DefaultDays > 0 && t.IsEnabled)
            .ToListAsync();

        var activeUsers = await _context.Users.Where(u => u.IsActive).ToListAsync();

        foreach (var user in activeUsers)
        {
            foreach (var type in leaveTypes)
            {
                var hasAllocation = await _context.LeaveAllocations
                    .AnyAsync(a => a.EmployeeId == user.Id && a.LeaveTypeId == type.Id && a.Period == year);

                if (!hasAllocation)
                {
                    _context.LeaveAllocations.Add(new LeaveAllocation
                    {
                        EmployeeId = user.Id,
                        LeaveTypeId = type.Id,
                        NumberOfDays = type.DefaultDays,
                        Period = year,
                        DateCreated = DateTime.UtcNow,
                        DateModified = DateTime.UtcNow
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
