using pulse.Models;

namespace pulse.Constants;

public class EmailTemplates
{
    public static string Welcome(string userEmail) => $"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
      <title>Welcome to Pulse</title>
    </head>
    <body style="margin:0;padding:0;background-color:#0a0a0a;font-family:'Helvetica Neue',Arial,sans-serif;">
      <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0a;padding:40px 16px;">
        <tr>
          <td align="center">
            <table width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;">

              <!-- Logo -->
              <tr>
                <td align="center" style="padding-bottom:32px;">
                  <svg width="48" height="42" viewBox="0 0 104.57499 91.598297" xmlns="http://www.w3.org/2000/svg">
                    <g transform="translate(-30.6074,-158.97283)">
                      <g transform="translate(-1.0593226,3.4427986)">
                        <path style="fill:#ffffff;" d="m 31.666724,155.53004 32.143821,0.0662 20.160233,34.46109 20.458172,-34.32867 h 31.81278 l -52.403365,91.39968 z"/>
                        <path style="fill:black;" d="m 47.523459,165.69292 12.149108,0.16552 24.364421,42.17428 24.960292,-42.47222 11.48703,0.23173 -36.348011,63.59246 z"/>
                      </g>
                    </g>
                  </svg>
                  <p style="margin:8px 0 0;color:#f9fafb;font-size:18px;font-weight:600;letter-spacing:-0.3px;">Pulse</p>
                </td>
              </tr>

              <!-- Card -->
              <tr>
                <td style="background-color:#111111;border:1px solid rgba(255,255,255,0.08);border-radius:12px;padding:40px 36px;">

                  <h1 style="margin:0 0 12px;color:#f9fafb;font-size:22px;font-weight:600;letter-spacing:-0.3px;">
                    Welcome to Pulse 👋
                  </h1>
                  <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;">
                    Your account is ready. Pulse gives you clean, privacy-friendly analytics for your websites. No cookies, no tracking bloat.
                  </p>

                  <!-- Steps -->
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                    <tr>
                      <td style="padding:14px 0;border-bottom:1px solid rgba(255,255,255,0.06);">
                        <p style="margin:0;color:#f9fafb;font-size:14px;font-weight:500;">① Create a project</p>
                        <p style="margin:4px 0 0;color:#6b7280;font-size:13px;">Add your website and get a tracking script.</p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:14px 0;border-bottom:1px solid rgba(255,255,255,0.06);">
                        <p style="margin:0;color:#f9fafb;font-size:14px;font-weight:500;">② Install the script</p>
                        <p style="margin:4px 0 0;color:#6b7280;font-size:13px;">Paste one line into your site's &lt;head&gt; tag.</p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:14px 0;">
                        <p style="margin:0;color:#f9fafb;font-size:14px;font-weight:500;">③ Watch the data come in</p>
                        <p style="margin:4px 0 0;color:#6b7280;font-size:13px;">Your dashboard updates in real time.</p>
                      </td>
                    </tr>
                  </table>

                  <!-- CTA -->
                  <table width="100%" cellpadding="0" cellspacing="0">
                    <tr>
                      <td align="center">
                        <a href="https://pulse.velovix.com/dashboard"
                           style="display:inline-block;background-color:#3b82f6;color:#ffffff;font-size:14px;font-weight:600;text-decoration:none;padding:12px 28px;border-radius:8px;">
                          Go to Dashboard
                        </a>
                      </td>
                    </tr>
                  </table>

                </td>
              </tr>

              <!-- Footer -->
              <tr>
                <td align="center" style="padding-top:28px;">
                  <p style="margin:0;color:#6b7280;font-size:12px;line-height:1.6;">
                    You're receiving this because you signed up at
                    <a href="https://pulse.velovix.com" style="color:#3b82f6;text-decoration:none;">pulse.velovix.com</a>
                    with {userEmail}.
                  </p>
                  <p style="margin:8px 0 0;color:#6b7280;font-size:12px;">
                    &copy; {DateTime.UtcNow.Year} Pulse by
                    <a href="https://velovix.com" style="color:#3b82f6;text-decoration:none;">Velovix</a>
                  </p>
                </td>
              </tr>

            </table>
          </td>
        </tr>
      </table>
    </body>
    </html>
    """;

    public static string PasswordResetEmail(string code) => $"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
      <title>Reset your password</title>
    </head>
    <body style="margin:0;padding:0;background-color:#0a0a0a;font-family:'Helvetica Neue',Arial,sans-serif;">
      <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0a;padding:40px 16px;">
        <tr>
          <td align="center">
            <table width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;">

              <!-- Logo -->
              <tr>
                <td align="center" style="padding-bottom:32px;">
                  <svg width="48" height="42" viewBox="0 0 104.57499 91.598297" xmlns="http://www.w3.org/2000/svg">
                    <g transform="translate(-30.6074,-158.97283)">
                      <g transform="translate(-1.0593226,3.4427986)">
                        <path style="fill:#ffffff;" d="m 31.666724,155.53004 32.143821,0.0662 20.160233,34.46109 20.458172,-34.32867 h 31.81278 l -52.403365,91.39968 z"/>
                        <path style="fill:#3b82f6;" d="m 47.523459,165.69292 12.149108,0.16552 24.364421,42.17428 24.960292,-42.47222 11.48703,0.23173 -36.348011,63.59246 z"/>
                      </g>
                    </g>
                  </svg>
                  <p style="margin:8px 0 0;color:#f9fafb;font-size:18px;font-weight:600;letter-spacing:-0.3px;">Pulse</p>
                </td>
              </tr>

              <!-- Card -->
              <tr>
                <td style="background-color:#111111;border:1px solid rgba(255,255,255,0.08);border-radius:12px;padding:40px 36px;">

                  <h1 style="margin:0 0 12px;color:#f9fafb;font-size:22px;font-weight:600;letter-spacing:-0.3px;">
                    Reset your password
                  </h1>
                  <p style="margin:0 0 28px;color:#6b7280;font-size:15px;line-height:1.6;">
                    We received a request to reset the password for your Pulse account. Use the code below to proceed. This code expires in <span style="color:#f9fafb;font-weight:500;">15 minutes</span>.
                  </p>

                  <!-- Code box -->
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                    <tr>
                      <td align="center">
                        <div style="display:inline-block;background-color:#0a0a0a;border:1px solid rgba(59,130,246,0.3);border-radius:10px;padding:20px 40px;">
                          <p style="margin:0;color:#3b82f6;font-size:32px;font-weight:700;letter-spacing:10px;">{code}</p>
                        </div>
                      </td>
                    </tr>
                  </table>

                  <p style="margin:0;color:#6b7280;font-size:13px;line-height:1.6;text-align:center;">
                    If you didn't request a password reset, you can safely ignore this email. Your password will not be changed.
                  </p>

                </td>
              </tr>

              <!-- Footer -->
              <tr>
                <td align="center" style="padding-top:28px;">
                  <p style="margin:0;color:#6b7280;font-size:12px;line-height:1.6;">
                    &copy; {DateTime.UtcNow.Year} Pulse by
                    <a href="https://velovix.com" style="color:#3b82f6;text-decoration:none;">Velovix</a>
                  </p>
                </td>
              </tr>

            </table>
          </td>
        </tr>
      </table>
    </body>
    </html>
    """;

    public static string WeeklyReportEmail(string userEmail, List<ProjectWeeklyReport> projects) => $"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
      <title>Your Weekly Pulse Report</title>
    </head>
    <body style="margin:0;padding:0;background-color:#0a0a0a;font-family:'Helvetica Neue',Arial,sans-serif;">
      <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0a;padding:40px 16px;">
        <tr>
          <td align="center">
            <table width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;">

              <!-- Logo -->
              <tr>
                <td align="center" style="padding-bottom:32px;">
                  <svg width="48" height="42" viewBox="0 0 104.57499 91.598297" xmlns="http://www.w3.org/2000/svg">
                    <g transform="translate(-30.6074,-158.97283)">
                      <g transform="translate(-1.0593226,3.4427986)">
                        <path style="fill:#ffffff;" d="m 31.666724,155.53004 32.143821,0.0662 20.160233,34.46109 20.458172,-34.32867 h 31.81278 l -52.403365,91.39968 z"/>
                        <path style="fill:#3b82f6;" d="m 47.523459,165.69292 12.149108,0.16552 24.364421,42.17428 24.960292,-42.47222 11.48703,0.23173 -36.348011,63.59246 z"/>
                      </g>
                    </g>
                  </svg>
                  <p style="margin:8px 0 0;color:#f9fafb;font-size:18px;font-weight:600;letter-spacing:-0.3px;">Pulse</p>
                </td>
              </tr>

              <!-- Header Card -->
              <tr>
                <td style="background-color:#111111;border:1px solid rgba(255,255,255,0.08);border-radius:12px 12px 0 0;padding:32px 36px 24px;">
                  <p style="margin:0 0 4px;color:#6b7280;font-size:12px;font-weight:500;text-transform:uppercase;letter-spacing:1px;">Weekly Report</p>
                  <h1 style="margin:0;color:#f9fafb;font-size:22px;font-weight:600;letter-spacing:-0.3px;">
                    Here's your week in review
                  </h1>
                  <p style="margin:8px 0 0;color:#6b7280;font-size:13px;">
                    {DateTime.UtcNow.AddDays(-7).ToString("MMM d")} – {DateTime.UtcNow.ToString("MMM d, yyyy")}
                  </p>
                </td>
              </tr>

              <!-- Projects -->
              {string.Join("\n", projects.Select((project, i) => $"""
              <tr>
                <td style="background-color:#111111;border-left:1px solid rgba(255,255,255,0.08);border-right:1px solid rgba(255,255,255,0.08);border-bottom:1px solid rgba(255,255,255,0.08);{(i == projects.Count - 1 ? "border-radius:0 0 12px 12px;" : "")}padding:24px 36px;">

                  <!-- Project name -->
                  <p style="margin:0 0 16px;color:#f9fafb;font-size:15px;font-weight:600;border-bottom:1px solid rgba(255,255,255,0.06);padding-bottom:12px;">
                    {project.ProjectName}
                  </p>

                  <!-- Views stat -->
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
                    <tr>
                      <td style="background-color:#0a0a0a;border:1px solid rgba(255,255,255,0.06);border-radius:8px;padding:16px 20px;">
                        <p style="margin:0 0 4px;color:#6b7280;font-size:12px;">Total views this week</p>
                        <p style="margin:0;color:#f9fafb;font-size:28px;font-weight:700;">{project.TotalViewsThisWeek.ToString("N0")}</p>
                        <p style="margin:4px 0 0;font-size:12px;color:{(project.PercentChange >= 0 ? "#22c55e" : "#ef4444")};">
                          {(project.PercentChange >= 0 ? "▲" : "▼")} {Math.Abs(project.PercentChange)}% vs last week
                        </p>
                      </td>
                    </tr>
                  </table>

                  <!-- Top Pages -->
                  {(project.TopPages.Count > 0 ? $"""
                  <p style="margin:0 0 8px;color:#6b7280;font-size:12px;font-weight:500;text-transform:uppercase;letter-spacing:0.8px;">Top Pages</p>
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
                    {string.Join("\n", project.TopPages.Select(p => $"""
                    <tr>
                      <td style="padding:6px 0;border-bottom:1px solid rgba(255,255,255,0.04);">
                        <table width="100%" cellpadding="0" cellspacing="0">
                          <tr>
                            <td style="color:#f9fafb;font-size:13px;word-break:break-all;">{p.Url}</td>
                            <td align="right" style="color:#6b7280;font-size:13px;white-space:nowrap;padding-left:12px;">{p.Count} views</td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                    """))}
                  </table>
                  """ : "")}

                  <!-- Top Referrers -->
                  {(project.TopReferrers.Count > 0 ? $"""
                  <p style="margin:0 0 8px;color:#6b7280;font-size:12px;font-weight:500;text-transform:uppercase;letter-spacing:0.8px;">Top Referrers</p>
                  <table width="100%" cellpadding="0" cellspacing="0">
                    {string.Join("\n", project.TopReferrers.Select(r => $"""
                    <tr>
                      <td style="padding:6px 0;border-bottom:1px solid rgba(255,255,255,0.04);">
                        <table width="100%" cellpadding="0" cellspacing="0">
                          <tr>
                            <td style="color:#f9fafb;font-size:13px;">{r.Referrer ?? "Direct"}</td>
                            <td align="right" style="color:#6b7280;font-size:13px;white-space:nowrap;padding-left:12px;">{r.Count} views</td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                    """))}
                  </table>
                  """ : "")}

                </td>
              </tr>
              """))}

              <!-- CTA -->
              <tr>
                <td align="center" style="padding:28px 0;">
                  <a href="https://pulse.velovix.com/dashboard"
                     style="display:inline-block;background-color:#3b82f6;color:#ffffff;font-size:14px;font-weight:600;text-decoration:none;padding:12px 28px;border-radius:8px;">
                    View Full Dashboard
                  </a>
                </td>
              </tr>

              <!-- Footer -->
              <tr>
                <td align="center">
                  <p style="margin:0;color:#6b7280;font-size:12px;line-height:1.6;">
                    You're receiving this because you have a Pro plan at
                    <a href="https://pulse.velovix.com" style="color:#3b82f6;text-decoration:none;">pulse.velovix.com</a>.
                  </p>
                  <p style="margin:8px 0 0;color:#6b7280;font-size:12px;">
                    &copy; {DateTime.UtcNow.Year} Pulse by
                    <a href="https://velovix.com" style="color:#3b82f6;text-decoration:none;">Velovix</a>
                  </p>
                </td>
              </tr>

            </table>
          </td>
        </tr>
      </table>
    </body>
    </html>
    """;
}
