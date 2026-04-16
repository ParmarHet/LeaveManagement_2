using LeavePro.Models;

namespace LeavePro.Services;

public interface ILeaveAllocationService
{
    Task AllocateDefaultLeavesAsync(string userId, int year);
    Task AllocateDefaultLeavesToAllActiveUsersAsync(int year);
}
