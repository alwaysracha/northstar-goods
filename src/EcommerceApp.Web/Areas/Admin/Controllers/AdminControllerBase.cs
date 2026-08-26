using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrator")]
public abstract class AdminControllerBase : Controller { }
