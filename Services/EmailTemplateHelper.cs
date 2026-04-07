using System.Text.Encodings.Web;

namespace LMS.Services;

public static class EmailTemplateHelper
{
    private const string PrimaryColor = "#4f46e5";
    private const string SuccessColor = "#22c55e";
    private const string DangerColor = "#ef4444";
    private const string BackgroundColor = "#f8f9fa";

    public static string GetBaseTemplate(string title, string content, string? actionUrl = null, string? actionText = null)
    {
        string actionButton = "";
        if (!string.IsNullOrEmpty(actionUrl) && !string.IsNullOrEmpty(actionText))
        {
            actionButton = $@"
                <tr>
                    <td align=""center"" style=""padding: 30px 0 10px 0;"">
                        <a href=""{actionUrl}"" style=""background-color: {PrimaryColor}; color: #ffffff; padding: 12px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; box-shadow: 0 4px 6px rgba(79, 70, 229, 0.2);"">
                            {actionText}
                        </a>
                    </td>
                </tr>";
        }

        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""utf-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            <title>{title}</title>
            <style>
                body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: {BackgroundColor}; margin: 0; padding: 0; -webkit-font-smoothing: antialiased; }}
                .container {{ max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.05); }}
                .header {{ background: linear-gradient(135deg, {PrimaryColor}, #6366f1); padding: 40px 20px; text-align: center; color: #ffffff; }}
                .content {{ padding: 40px; line-height: 1.6; color: #334155; }}
                .footer {{ background-color: #f1f5f9; padding: 20px; text-align: center; color: #64748b; font-size: 13px; }}
                .divider {{ height: 1px; background-color: #e2e8f0; margin: 25px 0; }}
                .info-box {{ background-color: #f8fafc; border-radius: 12px; padding: 20px; border: 1px solid #e2e8f0; margin: 20px 0; }}
                .badge {{ display: inline-block; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: bold; margin-bottom: 10px; }}
                .badge-pending {{ background-color: #fef3c7; color: #92400e; }}
                .badge-success {{ background-color: #dcfce7; color: #166534; }}
                .badge-danger {{ background-color: #fee2e2; color: #991b1b; }}
            </style>
        </head>
        <body>
            <div class=""container"">
                <div class=""header"">
                    <h1 style=""margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -0.5px;"">LMS Portal</h1>
                </div>
                <div class=""content"">
                    <h2 style=""margin-top: 0; color: #1e293b; font-size: 20px;"">{title}</h2>
                    {content}
                    <table width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                        {actionButton}
                    </table>
                    <div class=""divider""></div>
                    <p style=""font-size: 14px; margin-bottom: 0;"">Regards,<br><strong>LMS Administrator Team</strong></p>
                </div>
                <div class=""footer"">
                    <p style=""margin: 0;"">© {DateTime.Now.Year} Leave Management System. All rights reserved.</p>
                    <p style=""margin: 5px 0 0 0;"">This is an automated message, please do not reply.</p>
                </div>
            </div>
        </body>
        </html>";
    }

    public static string GetRegistrationRequestTemplate(string name, string email, string? code, DateTime joiningDate, string dashboardUrl)
    {
        string content = $@"
            <p>Hello Admin,</p>
            <p>A new user has registered and is awaiting your review for account activation.</p>
            <div class=""info-box"">
                <div class=""badge badge-pending"">Action Required</div>
                <table width=""100%"" style=""font-size: 15px;"">
                    <tr><td width=""40%"" style=""font-weight: bold; color: #64748b;"">Full Name:</td><td>{name}</td></tr>
                    <tr><td style=""font-weight: bold; color: #64748b;"">Email:</td><td>{email}</td></tr>
                    <tr><td style=""font-weight: bold; color: #64748b;"">Emp Code:</td><td>{code ?? "N/A"}</td></tr>
                    <tr><td style=""font-weight: bold; color: #64748b;"">Join Date:</td><td>{joiningDate:dd MMM yyyy}</td></tr>
                </table>
            </div>
            <p>Please review and assign the appropriate department and manager for this user to enable their access.</p>";

        return GetBaseTemplate("New User Activation Required", content, dashboardUrl, "Go to Pending Approvals");
    }

    public static string GetUserStatusUpdateTemplate(string name, string status, bool isApproved)
    {
        string badgeClass = isApproved ? "badge-success" : "badge-danger";
        string statusText = isApproved ? "Approved" : "Rejected";
        string message = isApproved 
            ? "Congratulations! Your account registration request has been approved. You can now log in to the portal."
            : "We regret to inform you that your registration request has been rejected. Please contact your manager or HR for further assistance.";

        string content = $@"
            <p>Hello {name},</p>
            <div class=""badge {badgeClass}"">{statusText}</div>
            <p>{message}</p>";

        return GetBaseTemplate("Registration Account Status", content);
    }

    public static string GetLeaveRequestTemplate(string employeeName, string leaveType, DateTime start, DateTime end, int days, string reason, string dashboardUrl)
    {
        string content = $@"
            <p>Hello,</p>
            <p>A new leave application has been submitted and requires your attention.</p>
            <div class=""info-box"">
                <table width=""100%"" style=""font-size: 15px;"">
                    <tr><td width=""40%"" style=""font-weight: bold; color: #64748b;"">Employee:</td><td>{employeeName}</td></tr>
                    <tr><td style=""font-weight: bold; color: #64748b;"">Leave Type:</td><td>{leaveType}</td></tr>
                    <tr><td style=""font-weight: bold; color: #64748b;"">Duration:</td><td>{start:dd MMM} - {end:dd MMM} ({days} days)</td></tr>
                    <tr><td style=""font-weight: bold; color: #64748b; vertical-align: top;"">Reason:</td><td>{reason}</td></tr>
                </table>
            </div>
            <p>Please review the request promptly to ensure department coverage is maintained.</p>";

        return GetBaseTemplate("Leave Request Received", content, dashboardUrl, "Review Leave Request");
    }

    public static string GetLeaveResponseTemplate(string name, string status, bool isApproved, string leaveType, DateTime start, string? remarks)
    {
        string badgeClass = isApproved ? "badge-success" : "badge-danger";
        string statusText = isApproved ? "Approved" : "Rejected";
        string remarkHtml = string.IsNullOrEmpty(remarks) ? "" : $@"<p><strong>Remarks:</strong> {remarks}</p>";

        string content = $@"
            <p>Hello {name},</p>
            <p>Your leave request for <strong>{leaveType}</strong> starting on <strong>{start:dd MMM yyyy}</strong> has been updated.</p>
            <div class=""badge {badgeClass}"">{statusText}</div>
            {remarkHtml}";

        return GetBaseTemplate("Leave Request Update", content);
    }
}
