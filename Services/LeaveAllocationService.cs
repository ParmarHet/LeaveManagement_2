using Microsoft.EntityFrameworkCore;
using LeavePro.Models;
using LeavePro.Data;

namespace LeavePro.Services;

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
                double days = type.DefaultDays;
                
                if (type.Code == "PL")
                {
                    // PL is calculated based on the joining date (1.5 per month since joining in the current year)
                    if (user.DateJoined.Year == year)
                    {
                        var currentMonth = DateTime.Now.Month;
                        var joinMonth = user.DateJoined.Month;
                        // Calculate months elapsed since joining (including the join month)
                        int monthsServed = Math.Max(1, currentMonth - joinMonth + 1);
                        days = monthsServed * 1.5;
                    }
                    else if (user.DateJoined.Year < year)
                    {
                        // User was already here at the start of the year, usually starts with 1.5 in Jan if doing yearly reset
                        // But since background service increments it, we give them the credit for elapsed months
                        days = DateTime.Now.Month * 1.5;
                    }
                    else
                    {
                        days = 0; // Future joiner?
                    }
                }
                // For SL, CL, and others, give the full yearly quota as requested
                
                _context.LeaveAllocations.Add(new LeaveAllocation
                {
                    EmployeeId = userId,
                    LeaveTypeId = type.Id,
                    NumberOfDays = days,
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
                    double days = type.DefaultDays;
                    
                    if (type.Code == "PL")
                    {
                        if (user.DateJoined.Year == year)
                        {
                            var currentMonth = DateTime.Now.Month;
                            var joinMonth = user.DateJoined.Month;
                            int monthsServed = Math.Max(1, currentMonth - joinMonth + 1);
                            days = monthsServed * 1.5;
                        }
                        else if (user.DateJoined.Year < year)
                        {
                            days = DateTime.Now.Month * 1.5;
                        }
                        else days = 0;
                    }

                    _context.LeaveAllocations.Add(new LeaveAllocation
                    {
                        EmployeeId = user.Id,
                        LeaveTypeId = type.Id,
                        NumberOfDays = days,
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
