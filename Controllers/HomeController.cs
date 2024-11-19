using Microsoft.AspNetCore.Mvc;
using Fixer_Common.Interfaces;
using System.Threading.Tasks;

namespace Fixer_Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public HomeController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            // Şimdilik Problems sayfasına yönlendirelim
            return RedirectToAction("Index", "Problems");
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
