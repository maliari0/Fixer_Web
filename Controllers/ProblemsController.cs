using Microsoft.AspNetCore.Mvc;
using Fixer_Common.Interfaces;
using Fixer_Common.Models;
using Fixer_Web.Models.ViewModels;
using AutoMapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fixer_Common.Models.Dto;
using System;
using Microsoft.Extensions.Logging;

namespace Fixer_Web.Controllers
{
    public class ProblemsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIService _aiService;
        private readonly IMapper _mapper;
        private readonly ILogger<ProblemsController> _logger;

        public ProblemsController(
            IUnitOfWork unitOfWork,
            IAIService aiService,
            IMapper mapper,
            ILogger<ProblemsController> logger)
        {
            _unitOfWork = unitOfWork;
            _aiService = aiService;
            _mapper = mapper;
            _logger = logger;
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
        public async Task<IActionResult> Create(CreateProblemDto model)
        {
            try
            {
                // Önce Problem'i oluştur
                var problem = new Problem
                {
                    Description = model.Description,
                    Category = model.Category,
                    OperatingSystem = model.OperatingSystem,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Problems.AddAsync(problem);
                await _unitOfWork.SaveChangesAsync();

                // AI'dan çözüm al
                var aiSolution = await _aiService.GenerateSolutionAsync(new AIRequestDto
                {
                    ProblemDescription = model.Description,
                    Category = model.Category,
                    OperatingSystem = model.OperatingSystem
                });

                // Solution'ı Problem'e bağla
                var solution = new Solution
                {
                    ProblemId = problem.Id, // Problem ile ilişkilendir
                    SolutionText = aiSolution,
                    Category = model.Category,
                    OperatingSystem = model.OperatingSystem,
                    SuccessCount = 0,
                    TotalUsageCount = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                await _unitOfWork.Solutions.AddAsync(solution);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation($"Created problem and solution. ProblemId: {problem.Id}, SolutionId: {solution.Id}");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating problem and solution");
                ModelState.AddModelError("", "Sorun oluşturulurken bir hata meydana geldi.");
                return View(model);
            }
        }
    }
} 