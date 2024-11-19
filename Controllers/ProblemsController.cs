using Microsoft.AspNetCore.Mvc;
using Fixer_Common.Interfaces;
using Fixer_Common.Models;
using Fixer_Web.Models.ViewModels;
using AutoMapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fixer_Common.Models.Dto;
using System;

namespace Fixer_Web.Controllers
{
    public class ProblemsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIService _aiService;
        private readonly IMapper _mapper;

        public ProblemsController(
            IUnitOfWork unitOfWork,
            IAIService aiService,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _aiService = aiService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            var problems = await _unitOfWork.Problems.GetAllAsync();
            var viewModel = _mapper.Map<List<ProblemListViewModel>>(problems);
            return View(viewModel);
        }

        public async Task<IActionResult> Details(int id)
        {
            var problem = await _unitOfWork.Problems.GetByIdAsync(id);
            if (problem == null)
                return NotFound();

            var viewModel = _mapper.Map<ProblemDetailViewModel>(problem);
            return View(viewModel);
        }

        public IActionResult Create()
        {
            return View(new ProblemCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProblemCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Create problem
                var problem = _mapper.Map<Problem>(model);
                await _unitOfWork.Problems.AddAsync(problem);
                await _unitOfWork.SaveChangesAsync();

                // Generate AI solution
                var aiRequest = new AIRequestDto
                {
                    ProblemDescription = model.Description,
                    Category = model.Category,
                    OperatingSystem = model.OperatingSystem
                };

                var aiSolution = await _aiService.GenerateSolutionAsync(aiRequest);

                // Save AI solution
                var solution = new Solution
                {
                    ProblemDescription = model.Description,
                    Category = model.Category,
                    OperatingSystem = model.OperatingSystem,
                    SolutionText = aiSolution,
                    SuccessCount = 0,
                    TotalUsageCount = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                await _unitOfWork.Solutions.AddAsync(solution);
                await _unitOfWork.SaveChangesAsync();

                // Başarı mesajını TempData'ya ekleyelim
                TempData["SuccessMessage"] = "Sorun başarıyla oluşturuldu!";
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(model);
            }
        }
    }
} 