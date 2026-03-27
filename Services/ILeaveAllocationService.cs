using LMS.Models;

namespace LMS.Services;

public interface ILeaveAllocationService
{
    Task AllocateDefaultLeavesAsync(string userId, int year);
    Task AllocateDefaultLeavesToAllActiveUsersAsync(int year);
}
