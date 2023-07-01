// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Prisa.DataAccess.Repository.IRepository;
using Prisa.Models;
using Prisa.Utility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace PrisaWeb.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IUnitOfWork _unitOfWork;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _roleManager=roleManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }


            public string? Role { get; set; }
            [ValidateNever]
            public IEnumerable<SelectListItem> RoleList { get; set; }

            [Required]
            public string Name { get; set; }
            public string? StreetAddress { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? PostalCode { get; set; }
            public string? PhoneNumber { get; set; }
            public int? CompanyId { get; set; }
            [ValidateNever]
            public IEnumerable<SelectListItem> CompanyList { get; set; }

        }


        public async Task OnGetAsync(string returnUrl = null)
        {
           

            Input = new() {
                RoleList = _roleManager.Roles.Select(x => x.Name).Select(i => new SelectListItem {
                    Text = i,
                    Value = i
                }),
                CompanyList = _unitOfWork.Company.GetAll().Select(i => new SelectListItem {
                    Text = i.Name,
                    Value = i.Id.ToString()
                })
            };

            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();
                
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                user.StreetAddress = Input.StreetAddress;
                user.City = Input.City;
                user.Name = Input.Name;
                user.State = Input.State;
                user.PostalCode = Input.PostalCode;
                user.PhoneNumber = Input.PhoneNumber;

                if (Input.Role == SD.Role_Company) {
                    user.CompanyId=Input.CompanyId;
                }

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded) {
                    _logger.LogInformation("User created a new account with password.");

                    if (!String.IsNullOrEmpty(Input.Role)) {
                        await _userManager.AddToRoleAsync(user, Input.Role);
                    }
                    else {
                        await _userManager.AddToRoleAsync(user, SD.Role_Customer);
                    }

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
						 $"<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">\r\n<html data-editor-version=\"2\" class=\"sg-campaigns\" xmlns=\"http://www.w3.org/1999/xhtml\">\r\n    <head>\r\n      <meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">\r\n      <meta name=\"viewport\" content=\"width=device-width, initial-scale=1, minimum-scale=1, maximum-scale=1\">\r\n      <!--[if !mso]><!-->\r\n      <meta http-equiv=\"X-UA-Compatible\" content=\"IE=Edge\">\r\n      <!--<![endif]-->\r\n      <!--[if (gte mso 9)|(IE)]>\r\n      <xml>\r\n        <o:OfficeDocumentSettings>\r\n          <o:AllowPNG/>\r\n          <o:PixelsPerInch>96</o:PixelsPerInch>\r\n        </o:OfficeDocumentSettings>\r\n      </xml>\r\n      <![endif]-->\r\n      <!--[if (gte mso 9)|(IE)]>\r\n  <style type=\"text/css\">\r\n    body {{width: 600px;margin: 0 auto;}}\r\n    table {{border-collapse: collapse;}}\r\n    table, td {{mso-table-lspace: 0pt;mso-table-rspace: 0pt;}}\r\n    img {{-ms-interpolation-mode: bicubic;}}\r\n  </style>\r\n<![endif]-->\r\n      <style type=\"text/css\">\r\n    body, p, div {{\r\n      font-family: inherit;\r\n      font-size: 14px;\r\n    }}\r\n    body {{\r\n      color: #000000;\r\n    }}\r\n    body a {{\r\n      color: #1188E6;\r\n      text-decoration: none;\r\n    }}\r\n    p {{ margin: 0; padding: 0; }}\r\n    table.wrapper {{\r\n      width:100% !important;\r\n      table-layout: fixed;\r\n      -webkit-font-smoothing: antialiased;\r\n      -webkit-text-size-adjust: 100%;\r\n      -moz-text-size-adjust: 100%;\r\n      -ms-text-size-adjust: 100%;\r\n    }}\r\n    img.max-width {{\r\n      max-width: 100% !important;\r\n    }}\r\n    .column.of-2 {{\r\n      width: 50%;\r\n    }}\r\n    .column.of-3 {{\r\n      width: 33.333%;\r\n    }}\r\n    .column.of-4 {{\r\n      width: 25%;\r\n    }}\r\n    ul ul ul ul  {{\r\n      list-style-type: disc !important;\r\n    }}\r\n    ol ol {{\r\n      list-style-type: lower-roman !important;\r\n    }}\r\n    ol ol ol {{\r\n      list-style-type: lower-latin !important;\r\n    }}\r\n    ol ol ol ol {{\r\n      list-style-type: decimal !important;\r\n    }}\r\n    @media screen and (max-width:480px) {{\r\n      .preheader .rightColumnContent,\r\n      .footer .rightColumnContent {{\r\n        text-align: left !important;\r\n      }}\r\n      .preheader .rightColumnContent div,\r\n      .preheader .rightColumnContent span,\r\n      .footer .rightColumnContent div,\r\n      .footer .rightColumnContent span {{\r\n        text-align: left !important;\r\n      }}\r\n      .preheader .rightColumnContent,\r\n      .preheader .leftColumnContent {{\r\n        font-size: 80% !important;\r\n        padding: 5px 0;\r\n      }}\r\n      table.wrapper-mobile {{\r\n        width: 100% !important;\r\n        table-layout: fixed;\r\n      }}\r\n      img.max-width {{\r\n        height: auto !important;\r\n        max-width: 100% !important;\r\n      }}\r\n      a.bulletproof-button {{\r\n        display: block !important;\r\n        width: auto !important;\r\n        font-size: 80%;\r\n        padding-left: 0 !important;\r\n        padding-right: 0 !important;\r\n      }}\r\n      .columns {{\r\n        width: 100% !important;\r\n      }}\r\n      .column {{\r\n        display: block !important;\r\n        width: 100% !important;\r\n        padding-left: 0 !important;\r\n        padding-right: 0 !important;\r\n        margin-left: 0 !important;\r\n        margin-right: 0 !important;\r\n      }}\r\n      .social-icon-column {{\r\n        display: inline-block !important;\r\n      }}\r\n    }}\r\n  </style>\r\n      <!--user entered Head Start--><link href=\"https://fonts.googleapis.com/css?family=Muli&display=swap\" rel=\"stylesheet\"><style>\r\nbody {{font-family: 'Muli', sans-serif;}}\r\n</style><!--End Head user entered-->\r\n    </head>\r\n    <body>\r\n      <center class=\"wrapper\" data-link-color=\"#1188E6\" data-body-style=\"font-size:14px; font-family:inherit; color:#000000; background-color:#FFFFFF;\">\r\n        <div class=\"webkit\">\r\n          <table cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" class=\"wrapper\" bgcolor=\"#FFFFFF\">\r\n            <tr>\r\n              <td valign=\"top\" bgcolor=\"#FFFFFF\" width=\"100%\">\r\n                <table width=\"100%\" role=\"content-container\" class=\"outer\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">\r\n                  <tr>\r\n                    <td width=\"100%\">\r\n                      <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">\r\n                        <tr>\r\n                          <td>\r\n                            <!--[if mso]>\r\n    <center>\r\n    <table><tr><td width=\"600\">\r\n  <![endif]-->\r\n                                    <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%; max-width:600px;\" align=\"center\">\r\n                                      <tr>\r\n                                        <td role=\"modules-container\" style=\"padding:0px 0px 0px 0px; color:#000000; text-align:left;\" bgcolor=\"#FFFFFF\" width=\"100%\" align=\"left\"><table class=\"module preheader preheader-hide\" role=\"module\" data-type=\"preheader\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"display: none !important; mso-hide: all; visibility: hidden; opacity: 0; color: transparent; height: 0; width: 0;\">\r\n    <tr>\r\n      <td role=\"module-content\">\r\n        <p></p>\r\n      </td>\r\n    </tr>\r\n  </table>\r\n  <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" align=\"center\" width=\"100%\" role=\"module\" data-type=\"columns\" style=\"padding:30px 20px 30px 20px;\" bgcolor=\"#f6f6f6\" data-distribution=\"1\">\r\n    <tbody>\r\n      <tr role=\"module-content\">\r\n        <td height=\"100%\" valign=\"top\"><table width=\"540\" style=\"width:540px; border-spacing:0; border-collapse:collapse; margin:0px 10px 0px 10px;\" cellpadding=\"0\" cellspacing=\"0\" align=\"left\" border=\"0\" bgcolor=\"\" class=\"column column-0\">\r\n      <tbody>\r\n        <tr>\r\n          <td style=\"padding:0px;margin:0px;border-spacing:0;\"><table class=\"wrapper\" role=\"module\" data-type=\"image\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"72aac1ba-9036-4a77-b9d5-9a60d9b05cba\">\r\n    <tbody>\r\n      <!-- <tr>\r\n        <td style=\"font-size:6px; line-height:10px; padding:0px 0px 0px 0px;\" valign=\"top\" align=\"center\">\r\n          <img class=\"max-width\" border=\"0\" style=\"display:block; color:#000000; text-decoration:none; font-family:Helvetica, arial, sans-serif; font-size:16px;\" width=\"29\" alt=\"Prisa\" data-proportionally-constrained=\"true\" data-responsive=\"false\" src=\"https://cdn.discordapp.com/attachments/1116218780259991642/1116218839466774648/icon_1.png\" height=\"300\">\r\n        </td>\r\n      </tr> -->\r\n    </tbody>\r\n  </table><table class=\"module\" role=\"module\" data-type=\"spacer\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"331cde94-eb45-45dc-8852-b7dbeb9101d7\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"padding:0px 0px 20px 0px;\" role=\"module-content\" bgcolor=\"\">\r\n        </td>\r\n      </tr>\r\n    </tbody>\r\n  </table><table class=\"wrapper\" role=\"module\" data-type=\"image\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"d8508015-a2cb-488c-9877-d46adf313282\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"font-size:6px; line-height:10px; padding:0px 0px 0px 0px;\" valign=\"top\" align=\"center\">\r\n          <img class=\"max-width\" border=\"0\" style=\"display:block; color:#000000; text-decoration:none; font-family:Helvetica, arial, sans-serif; font-size:16px;\" width=\"50\" alt=\"\" data-proportionally-constrained=\"true\" data-responsive=\"false\" src=\"https://cdn.discordapp.com/attachments/1116218780259991642/1116218839466774648/icon_1.png\" height=\"50\">\r\n        </td>\r\n      </tr>\r\n    </tbody>\r\n  </table><table class=\"module\" role=\"module\" data-type=\"spacer\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"27716fe9-ee64-4a64-94f9-a4f28bc172a0\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"padding:0px 0px 30px 0px;\" role=\"module-content\" bgcolor=\"\">\r\n        </td>\r\n      </tr>\r\n    </tbody>\r\n  </table><table class=\"module\" role=\"module\" data-type=\"text\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"948e3f3f-5214-4721-a90e-625a47b1c957\" data-mc-module-version=\"2019-10-22\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"padding:50px 30px 18px 30px; line-height:36px; text-align:inherit; background-color:#ffffff;\" height=\"100%\" valign=\"top\" bgcolor=\"#ffffff\" role=\"module-content\"><div><div style=\"font-family: inherit; text-align: center\"><span style=\"font-size: 43px\">Thanks for signing up,{user.Name}!</span></div><div></div></div></td>\r\n      </tr>\r\n    </tbody>\r\n  </table><table class=\"module\" role=\"module\" data-type=\"text\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"a10dcb57-ad22-4f4d-b765-1d427dfddb4e\" data-mc-module-version=\"2019-10-22\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"padding:18px 30px 18px 30px; line-height:22px; text-align:inherit; background-color:#ffffff;\" height=\"100%\" valign=\"top\" bgcolor=\"#ffffff\" role=\"module-content\"><div><div style=\"font-family: inherit; text-align: center\"><span style=\"font-size: 18px\">Please verify your email address to</span><span style=\"color: #000000; font-size: 18px; font-family: arial, helvetica, sans-serif\"> get access to thousands of exclusive job listings</span><span style=\"font-size: 18px\">.</span></div>\r\n<div style=\"font-family: inherit; text-align: center\"><span style=\"color: #ffbe00; font-size: 18px\"><strong>Thank you!</strong></span></div><div></div></div></td>\r\n      </tr>\r\n    </tbody>\r\n  </table><table class=\"module\" role=\"module\" data-type=\"spacer\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"7770fdab-634a-4f62-a277-1c66b2646d8d\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"padding:0px 0px 20px 0px;\" role=\"module-content\" bgcolor=\"#ffffff\">\r\n        </td>\r\n      </tr>\r\n    </tbody>\r\n  </table><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"module\" data-role=\"module-button\" data-type=\"button\" role=\"module\" style=\"table-layout:fixed;\" width=\"100%\" data-muid=\"d050540f-4672-4f31-80d9-b395dc08abe1\">\r\n      <tbody>\r\n        <tr>\r\n          <td align=\"center\" bgcolor=\"#ffffff\" class=\"outer-td\" style=\"padding:0px 0px 0px 0px; background-color:#ffffff;\">\r\n            <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"wrapper-mobile\" style=\"text-align:center;\">\r\n              <tbody>\r\n                <tr>\r\n                <td align=\"center\" bgcolor=\"#ffbe00\" class=\"inner-td\" style=\"border-radius:6px; font-size:16px; text-align:center; background-color:inherit;\">\r\n                  <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style=\"background-color:#ffbe00; border:1px solid #ffbe00; border-color:#ffbe00; border-radius:0px; border-width:1px; color:#000000; display:inline-block; font-size:14px; font-weight:normal; letter-spacing:0px; line-height:normal; padding:12px 40px 12px 40px; text-align:center; text-decoration:none; border-style:solid; font-family:inherit;\" target=\"_blank\">Verify Email Now</a>\r\n                </td>\r\n                </tr>\r\n              </tbody>\r\n            </table>\r\n          </td>\r\n        </tr>\r\n      </tbody>\r\n    </table><table class=\"module\" role=\"module\" data-type=\"spacer\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout: fixed;\" data-muid=\"7770fdab-634a-4f62-a277-1c66b2646d8d.1\">\r\n    <tbody>\r\n      <tr>\r\n        <td style=\"padding:0px 0px 50px 0px;\" role=\"module-content\" bgcolor=\"#ffffff\">\r\n        </td>\r\n      </tr>\r\n    </tbody>\r\n  </table>\r\n  </td>\r\n        </tr>\r\n      </tbody>\r\n    </table></td>\r\n      </tr>\r\n    </tbody>\r\n  </table>\r\n <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"module\" data-role=\"module-button\" data-type=\"button\" role=\"module\" style=\"table-layout:fixed;\" width=\"100%\" data-muid=\"550f60a9-c478-496c-b705-077cf7b1ba9a\">\r\n      <tbody>\r\n        <tr>\r\n          <td align=\"center\" bgcolor=\"\" class=\"outer-td\" style=\"padding:0px 0px 20px 0px;\">\r\n         \r\n          </td>\r\n        </tr>\r\n      </tbody>\r\n    </table></td>\r\n                                      </tr>\r\n                                    </table>\r\n                                    <!--[if mso]>\r\n                                  </td>\r\n                                </tr>\r\n                              </table>\r\n                            </center>\r\n                            <![endif]-->\r\n                          </td>\r\n                        </tr>\r\n                      </table>\r\n                    </td>\r\n                  </tr>\r\n                </table>\r\n              </td>\r\n            </tr>\r\n          </table>\r\n        </div>\r\n      </center>\r\n    </body>\r\n  </html>");

					if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        if (User.IsInRole(SD.Role_Admin)) {
                            TempData["success"] = "New User Created Successfully";
                        }
                        else {
                            await _signInManager.SignInAsync(user, isPersistent: false);
                        }
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
