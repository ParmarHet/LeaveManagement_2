using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Data;
using LMS.Models;
using LMS.Constants;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ─── SETTINGS & PROFILE ──────────────────────────────────────────────────
    public async Task<IActionResult> Settings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        
        ViewBag.User = user;
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        
        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        ViewBag.User = user;

        if (!ModelState.IsValid) return View(model);

        var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            foreach (var error in changePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["Success"] = "Your password has been changed successfully.";
        return RedirectToAction(nameof(Settings));
    }

    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        
        await _context.Entry(user).Reference(u => u.Department).LoadAsync();
        
        return View(user);
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        return await Settings(model);
    }

    // ─── DASHBOARD ──────────────────────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;
        var employees = await _userManager.GetUsersInRoleAsync(Roles.Employee);
        var managers  = await _userManager.GetUsersInRoleAsync(Roles.Manager);

        var onLeaveToday = await _context.LeaveRequests
            .Include(r => r.RequestingEmployee)
            .Include(r => r.LeaveType)
            .Where(r => r.Approved == true && !r.Cancelled
                     && r.StartDate <= today && r.EndDate >= today)
            .ToListAsync();

        var upcomingHolidays = await _context.Holidays
            .Where(h => h.Date >= today)
            .OrderBy(h => h.Date)
            .Take(5)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalEmployees        = employees.Count + managers.Count,
            TotalManagers         = managers.Count,
            ActiveLeaveRequests   = await _context.LeaveRequests.CountAsync(r => !r.Cancelled && r.Approved == true),
            PendingApprovals      = await _context.LeaveRequests.CountAsync(r => r.Approved == null && !r.Cancelled),
            TotalDepartments      = await _context.Departments.CountAsync(d => d.ParentDepartmentId == null),
            TotalSubDepartments   = await _context.Departments.CountAsync(d => d.ParentDepartmentId != null),
            EmployeesOnLeaveToday = onLeaveToday.Count,
            UpcomingHolidays      = upcomingHolidays.Count,
            EmployeesOnLeaveTodayList = onLeaveToday,
            UpcomingHolidayList       = upcomingHolidays
        };

        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(model);
    }

    // ─── ALL LEAVE REQUESTS ─────────────────────────────────────────────────
    public async Task<IActionResult> AllLeaveRequests()
    {
        var leaves = await GetMappedLeaves(null);
        return View(leaves);
    }

    // ─── PENDING APPROVALS ──────────────────────────────────────────────────
    public async Task<IActionResult> PendingApprovals()
    {
        var leaves = await GetMappedLeaves("Pending");
        return View(leaves);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? remarks)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null) return NotFound();

        leave.Approved = true;
        leave.ReviewerId = admin.Id;
        leave.ManagerRemarks = remarks;
        leave.DateActioned = DateTime.UtcNow;

        var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeId);
        if (leaveType != null)
        {
            if (leaveType.Code == "LW")
            {
                var fortyFiveDaysAgo = DateTime.Today.AddDays(-45);
                var recentLwps = await _context.LeaveRequests
                    .Where(r => r.RequestingEmployeeId == leave.RequestingEmployeeId && r.Approved == true && !r.Cancelled && r.LeaveTypeId == leave.LeaveTypeId && r.StartDate >= fortyFiveDaysAgo)
                    .ToListAsync();
                
                int totalLwpDays = 0;
                foreach(var l in recentLwps) 
                {
                    for (var d = l.StartDate; d <= l.EndDate; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) totalLwpDays++;
                }
                
                int currentLwpDays = 0;
                for (var d = leave.StartDate; d <= leave.EndDate; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) currentLwpDays++;

                if (totalLwpDays + currentLwpDays >= 15 && totalLwpDays < 15)
                {
                    var plType = await _context.LeaveTypes.FirstOrDefaultAsync(l => l.Code == "PL");
                    if (plType != null)
                    {
                        var plAlloc = await _context.LeaveAllocations.FirstOrDefaultAsync(a => a.EmployeeId == leave.RequestingEmployeeId && a.LeaveTypeId == plType.Id && a.Period == DateTime.Now.Year);
                        if (plAlloc != null && plAlloc.NumberOfDays > 0)
                        {
                            plAlloc.NumberOfDays -= 1;
                            _context.LeaveAllocations.Update(plAlloc);
                        }
                    }
                }
            }
            else if (leaveType.Code == "CO")
            {
                int businessDays = 0;
                for (var d = leave.StartDate; d <= leave.EndDate; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) businessDays++;
                businessDays -= await _context.Holidays.CountAsync(h => h.Date >= leave.StartDate && h.Date <= leave.EndDate);
                
                var unusedCOs = await _context.CompensatoryOffs.Where(c => c.EmployeeId == leave.RequestingEmployeeId && !c.IsUsed && c.ExpiryDate >= leave.EndDate).OrderBy(c => c.ExpiryDate).Take(businessDays).ToListAsync();
                foreach(var co in unusedCOs)
                {
                    co.IsUsed = true;
                    _context.CompensatoryOffs.Update(co);
                }
            }
        }

        await _context.SaveChangesAsync();

        // Notify Employee
        var notification = new Notification
        {
            UserId = leave.RequestingEmployeeId,
            Title = "Leave Approved (Admin)",
            Message = $"Your leave request for {leave.StartDate:dd MMM} has been Approved by Admin.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request approved successfully.";
        return RedirectToAction(nameof(LeaveHistory));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? remarks)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null) return NotFound();

        leave.Approved = false;
        leave.ReviewerId = admin.Id;
        leave.ManagerRemarks = remarks;
        leave.DateActioned = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify Employee
        var notification = new Notification
        {
            UserId = leave.RequestingEmployeeId,
            Title = "Leave Rejected (Admin)",
            Message = $"Your leave request for {leave.StartDate:dd MMM} has been Rejected by Admin.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request rejected.";
        return RedirectToAction(nameof(LeaveHistory));
    }

    // ─── LEAVE HISTORY (CONSOLIDATED) ───────────────────────────────────────
    public async Task<IActionResult> LeaveHistory(int? departmentId, DateTime? date, string? role, string? searchName, int? leaveTypeId)
    {
        var today = DateTime.Today;

        ViewBag.CurrentDept = departmentId;
        ViewBag.CurrentDate = date?.ToString("yyyy-MM-dd");
        ViewBag.CurrentRole = role;
        ViewBag.CurrentSearch = searchName;
        ViewBag.CurrentLeaveType = leaveTypeId;

        // Today's Leaves
        var todayLeaves = await GetMappedLeaves("Today");
        
        // Pending Leaves
        var pendingLeaves = await GetMappedLeaves("Pending");

        // Department-wise Stats
        var departments = await _context.Departments
            .Include(d => d.SubDepartments)
            .ToListAsync();

        var allUsers = await _userManager.Users.ToListAsync();
        var nonAdminUsers = new List<ApplicationUser>();
        foreach(var u in allUsers)
        {
            if (!await _userManager.IsInRoleAsync(u, Roles.Admin))
            {
                nonAdminUsers.Add(u);
            }
        }

        var deptStats = new List<DepartmentLeaveStatsViewModel>();
        foreach(var dept in departments)
        {
            // Count users in this department who are NOT admins
            var deptUsers = allUsers.Where(u => u.DepartmentId == dept.Id).ToList();
            int nonAdminCount = 0;
            foreach(var u in deptUsers)
            {
                if (!await _userManager.IsInRoleAsync(u, Roles.Admin))
                {
                    nonAdminCount++;
                }
            }

            deptStats.Add(new DepartmentLeaveStatsViewModel
            {
                DepartmentName = dept.Name,
                TotalEmployees = nonAdminCount,
                OnLeaveToday = _context.LeaveRequests.Count(r => 
                    r.RequestingEmployee!.DepartmentId == dept.Id 
                    && r.Approved == true && !r.Cancelled 
                    && r.StartDate <= today && r.EndDate >= today),
                PendingApprovals = _context.LeaveRequests.Count(r => 
                    r.RequestingEmployee!.DepartmentId == dept.Id 
                    && r.Approved == null && !r.Cancelled)
            });
        }
        deptStats = deptStats.OrderBy(d => d.DepartmentName).ToList();
        
        
        var allLeavesQuery = _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Include(r => r.Reviewer)
            .AsQueryable();

        if (departmentId.HasValue)
            allLeavesQuery = allLeavesQuery.Where(r => r.RequestingEmployee!.DepartmentId == departmentId);

        if (date.HasValue)
            allLeavesQuery = allLeavesQuery.Where(r => r.StartDate <= date && r.EndDate >= date);

        if (leaveTypeId.HasValue)
            allLeavesQuery = allLeavesQuery.Where(r => r.LeaveTypeId == leaveTypeId);

        if (!string.IsNullOrEmpty(searchName))
            allLeavesQuery = allLeavesQuery.Where(r => (r.RequestingEmployee!.FirstName + " " + r.RequestingEmployee.LastName).Contains(searchName));

        var reqLeaves = await allLeavesQuery.OrderByDescending(r => r.DateRequested).ToListAsync();
        var allLeavesMapped = new List<AdminLeaveRequestViewModel>();

        foreach (var r in reqLeaves)
        {
            if (!string.IsNullOrEmpty(role))
            {
                var rRoles = await _userManager.GetRolesAsync(r.RequestingEmployee!);
                var rRole = rRoles.FirstOrDefault() ?? "Employee";
                if (rRole != role) continue;
            }
            
            allLeavesMapped.Add(new AdminLeaveRequestViewModel
            {
                Id             = r.Id,
                EmployeeId     = r.RequestingEmployeeId,
                EmployeeName   = $"{r.RequestingEmployee?.FirstName} {r.RequestingEmployee?.LastName}",
                EmployeeEmail  = r.RequestingEmployee?.Email ?? "",
                LeaveTypeName  = r.LeaveType?.Name ?? "",
                StartDate      = r.StartDate,
                EndDate        = r.EndDate,
                TotalDays      = (int)(r.EndDate - r.StartDate).TotalDays + 1,
                Reason         = r.RequestComments,
                DateRequested  = r.DateRequested,
                Status         = r.Cancelled ? "Cancelled" : r.Approved == null ? "Pending" : r.Approved == true ? "Approved" : "Rejected",
                ManagerRemarks = r.ManagerRemarks,
                ReviewerName   = r.Reviewer != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : null,
                DateActioned   = r.DateActioned,
                AttachmentPath = r.AttachmentPath,
                Cancelled      = r.Cancelled
            });
        }

        ViewBag.DepartmentsList = departments;

        var model = new AdminLeaveHistoryViewModel
        {
            OnLeaveTodayCount = todayLeaves.Count,
            PendingApprovalsCount = pendingLeaves.Count,
            TotalEmployees = nonAdminUsers.Count,
            TotalAbsent = todayLeaves.Count,
            TodayLeaves = todayLeaves,
            PendingLeaves = pendingLeaves,
            AllLeaves = allLeavesMapped,
            DepartmentStats = deptStats,
            LeaveTypes = await _context.LeaveTypes.OrderBy(l => l.Name).ToListAsync()
        };

        return View(model);
    }

    // ─── EMPLOYEE LEAVE HISTORY ─────────────────────────────────────────────
    public async Task<IActionResult> EmployeeLeaveHistory(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var employee = await _userManager.FindByIdAsync(id);
        if (employee == null) return NotFound();

        var leaves = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Reviewer)
            .Where(r => r.RequestingEmployeeId == id)
            .OrderByDescending(r => r.DateRequested)
            .ToListAsync();

        // Calculate Balances
        var balances = new List<LeaveBalanceViewModel>();
        var year = DateTime.Now.Year;
        var allocations = await _context.LeaveAllocations
            .Include(a => a.LeaveType)
            .Where(a => a.EmployeeId == id && a.Period == year)
            .ToListAsync();
        
        var approvedRequests = await _context.LeaveRequests
            .Where(r => r.RequestingEmployeeId == id && r.Approved == true && !r.Cancelled && (r.StartDate.Year == year || r.EndDate.Year == year))
            .ToListAsync();

        foreach (var alloc in allocations)
        {
            // Do not show ML (Maternity) and BL (Bereavement) in balance cards as per request
            if (alloc.LeaveType?.Code == "ML" || alloc.LeaveType?.Code == "BL") continue;

            var used = approvedRequests
                .Where(r => r.LeaveTypeId == alloc.LeaveTypeId)
                .Sum(r => (r.EndDate - r.StartDate).Days + 1);

            balances.Add(new LeaveBalanceViewModel
            {
                LeaveTypeName = alloc.LeaveType?.Name ?? "Unknown",
                Allocated = (int)alloc.NumberOfDays,
                Used = used
            });
        }

        ViewBag.Balances = balances;
        ViewBag.Employee = employee;
        return View(leaves);
    }



    // ─── LEAVE STRUCTURE ────────────────────────────────────────────────────
    public async Task<IActionResult> LeaveStructure()
    {
        var leaveTypes = await _context.LeaveTypes.OrderBy(l => l.Name).ToListAsync();

        // If no leave types configured, seed sensible defaults so the admin UI is populated
        if (leaveTypes == null || !leaveTypes.Any())
        {
            var defaults = new List<LeaveType>
            {
                new LeaveType { Name = "Paid Leave", Code = "PL", DefaultDays = 18, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 10, YearlyLimit = 18, CarryForward = true, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow },
                new LeaveType { Name = "Sick Leave", Code = "SL", DefaultDays = 10, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 5, YearlyLimit = 10, CarryForward = false, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow },
                new LeaveType { Name = "Compensatory Off", Code = "CO", DefaultDays = 0, IsPaid = false, RequiresApproval = true, MaxConsecutiveDays = 0, YearlyLimit = 0, CarryForward = false, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow },
                new LeaveType { Name = "Floating Holiday", Code = "FD", DefaultDays = 0, IsPaid = true, RequiresApproval = false, MaxConsecutiveDays = 1, YearlyLimit = 1, CarryForward = false, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow },
                new LeaveType { Name = "Maternity Leave", Code = "ML", DefaultDays = 182, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 182, YearlyLimit = 182, CarryForward = false, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow },
                new LeaveType { Name = "Bereavement Leave", Code = "BL", DefaultDays = 3, IsPaid = true, RequiresApproval = true, MaxConsecutiveDays = 5, YearlyLimit = 5, CarryForward = false, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow },
                new LeaveType { Name = "Leave Without Pay", Code = "LW", DefaultDays = 0, IsPaid = false, RequiresApproval = true, MaxConsecutiveDays = 0, YearlyLimit = 0, CarryForward = false, IsEnabled = true, DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow }
            };

            _context.LeaveTypes.AddRange(defaults);
            await _context.SaveChangesAsync();

            leaveTypes = await _context.LeaveTypes.OrderBy(l => l.Name).ToListAsync();
        }

        // Ensure common missing fields have sensible display values (non-destructive)
        foreach (var lt in leaveTypes)
        {
            if (string.IsNullOrWhiteSpace(lt.Code) && !string.IsNullOrWhiteSpace(lt.Name))
            {
                // derive a short code from name words
                lt.Code = string.Concat(lt.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0])).ToUpper();
                lt.DateModified = DateTime.UtcNow;
            }
        }
        // Save only if we modified codes
        if (leaveTypes.Any(l => _context.Entry(l).State == EntityState.Modified))
        {
            await _context.SaveChangesAsync();
        }

        return View(leaveTypes);
    }

    [HttpGet]
    public async Task<IActionResult> EditLeaveType(int id)
    {
        var leaveType = await _context.LeaveTypes.FindAsync(id);
        if (leaveType == null) return NotFound();
        return View(leaveType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLeaveType(int id, LeaveType model)
    {
        if (id != model.Id) return NotFound();
        if (ModelState.IsValid)
        {
            var existing = await _context.LeaveTypes.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name               = model.Name;
            existing.Code               = model.Code;
            existing.DefaultDays        = model.DefaultDays;
            existing.IsPaid             = model.IsPaid;
            existing.RequiresApproval   = model.RequiresApproval;
            existing.MaxConsecutiveDays = model.MaxConsecutiveDays;
            existing.CarryForward       = model.CarryForward;
            existing.YearlyLimit        = model.YearlyLimit;
            existing.IsEnabled          = model.IsEnabled;
            existing.DateModified       = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Leave type rules updated successfully.";
            return RedirectToAction(nameof(LeaveStructure));
        }
        return View(model);
    }

    // ─── HOLIDAY CALENDAR ────────────────────────────────────────────────────
    public async Task<IActionResult> HolidayCalendar()
    {
        var holidays = await _context.Holidays
            .Include(h => h.Department)
            .OrderBy(h => h.Date)
            .ToListAsync();
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        ViewBag.TotalHolidays = holidays.Count;
        ViewBag.UpcomingCount = holidays.Count(h => h.Date >= DateTime.Today);
        ViewBag.FloatingCount = holidays.Count(h => h.IsFloating);
        return View(holidays);
    }

    [HttpGet]
    public IActionResult AddHoliday()
    {
        ViewBag.Departments = _context.Departments.OrderBy(d => d.Name).ToList();
        return View(new Holiday());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHoliday(Holiday model)
    {
        if (ModelState.IsValid)
        {
            if (model.DepartmentId == 0) model.DepartmentId = null;
            _context.Holidays.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Holiday added successfully.";
            return RedirectToAction(nameof(HolidayCalendar));
        }
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditHoliday(int id)
    {
        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday == null) return NotFound();
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(holiday);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHoliday(int id, Holiday model)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.Holidays.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = model.Name;
            existing.Date = model.Date;
            existing.IsRecurringYearly = model.IsRecurringYearly;
            existing.IsFloating = model.IsFloating;
            existing.DepartmentId = model.DepartmentId == 0 ? null : model.DepartmentId;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Holiday updated successfully.";
            return RedirectToAction(nameof(HolidayCalendar));
        }
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday != null)
        {
            _context.Holidays.Remove(holiday);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Holiday deleted successfully.";
        }
        return RedirectToAction(nameof(HolidayCalendar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportHolidays(IFormFile file, List<int>? departmentIds)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please upload a valid CSV file.";
            return RedirectToAction(nameof(HolidayCalendar));
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            string? line;
            bool isHeader = true;
            int count = 0;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (isHeader) { isHeader = false; continue; }
                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    var name = parts[0].Trim();
                    if (DateTime.TryParse(parts[1].Trim(), out var date) && !string.IsNullOrWhiteSpace(name))
                    {
                        bool isRecurring = parts.Length >= 3 &&
                            (parts[2].Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || parts[2].Trim() == "1");
                        bool isFloating = parts.Length >= 4 &&
                            (parts[3].Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || parts[3].Trim() == "1");

                        if (departmentIds != null && departmentIds.Any())
                        {
                            foreach (var deptId in departmentIds)
                            {
                                _context.Holidays.Add(new Holiday
                                {
                                    Name = name, Date = date,
                                    IsRecurringYearly = isRecurring, IsFloating = isFloating,
                                    DepartmentId = deptId
                                });
                                count++;
                            }
                        }
                        else
                        {
                            _context.Holidays.Add(new Holiday
                            {
                                Name = name, Date = date,
                                IsRecurringYearly = isRecurring, IsFloating = isFloating
                            });
                            count++;
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{count} holidays imported successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error importing holidays: " + ex.Message;
        }

        return RedirectToAction(nameof(HolidayCalendar));
    }

    // ─── HELPER: Map LeaveRequests to ViewModels ─────────────────────────────
    private async Task<List<AdminLeaveRequestViewModel>> GetMappedLeaves(string? filter)
    {
        var query = _context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.RequestingEmployee)
            .Include(r => r.Reviewer)
            .AsQueryable();

        query = filter switch
        {
            "Pending"  => query.Where(r => r.Approved == null && !r.Cancelled),
            "Approved" => query.Where(r => r.Approved == true && !r.Cancelled),
            "Rejected" => query.Where(r => r.Approved == false && !r.Cancelled),
            "Today"    => query.Where(r => r.Approved == true && !r.Cancelled && r.StartDate <= DateTime.Today && r.EndDate >= DateTime.Today),
            _          => query
        };

        var leaves = await query.OrderByDescending(r => r.DateRequested).ToListAsync();

        return leaves.Select(r => new AdminLeaveRequestViewModel
        {
            Id             = r.Id,
            EmployeeId     = r.RequestingEmployeeId,
            EmployeeName   = $"{r.RequestingEmployee?.FirstName} {r.RequestingEmployee?.LastName}",
            EmployeeEmail  = r.RequestingEmployee?.Email ?? "",
            LeaveTypeName  = r.LeaveType?.Name ?? "",
            StartDate      = r.StartDate,
            EndDate        = r.EndDate,
            TotalDays      = (int)(r.EndDate - r.StartDate).TotalDays + 1,
            Reason         = r.RequestComments,
            DateRequested  = r.DateRequested,
            Status         = r.Cancelled ? "Cancelled" : r.Approved == null ? "Pending" : r.Approved == true ? "Approved" : "Rejected",
            ManagerRemarks = r.ManagerRemarks,
            ReviewerName   = r.Reviewer != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : null,
            DateActioned   = r.DateActioned,
            AttachmentPath = r.AttachmentPath,
            Cancelled      = r.Cancelled
        }).ToList();
    }
}
