using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Fixer_Common.Interfaces;
using Fixer_Common.Models;
using Fixer_Common.Models.Dto;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Fixer_Web.Controllers
{
    public class ChatController : Controller
    {
        private readonly IAIService _aiService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IAIService aiService,
            IUnitOfWork unitOfWork,
            ILogger<ChatController> logger)
        {
            _aiService = aiService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetResponse([FromBody] AIRequestDto request)
        {
            try
            {
                var response = await _aiService.GenerateSolutionAsync(request);
                return Json(new { success = true, response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveVote([FromBody] VoteDto vote)
        {
            try
            {
                // Önce aynı problem tanımı için mevcut kaydı kontrol et
                var existingProblem = await _unitOfWork.Problems
                    .GetFirstOrDefaultAsync(p => 
                        p.Description.ToLower() == vote.ProblemDescription.ToLower() &&
                        p.Category == vote.Category &&
                        p.OperatingSystem == vote.OperatingSystem);

                Problem problem;
                if (existingProblem != null)
                {
                    _logger.LogInformation($"Using existing problem with ID: {existingProblem.Id}");
                    problem = existingProblem;
                }
                else
                {
                    problem = new Problem
                    {
                        Description = vote.ProblemDescription,
                        Category = vote.Category ?? "Genel",
                        OperatingSystem = vote.OperatingSystem ?? "Diğer",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Problems.AddAsync(problem);
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation($"Created new problem with ID: {problem.Id}");
                }

                // Aynı çözüm metni için mevcut solution'ı kontrol et
                var existingSolution = await _unitOfWork.Solutions
                    .GetFirstOrDefaultAsync(s => 
                        s.SolutionText == vote.SolutionText && 
                        s.ProblemId == problem.Id);

                Solution solution;
                if (existingSolution != null)
                {
                    // Var olan solution'ı güncelle
                    existingSolution.LastUsedAt = DateTime.UtcNow;
                    existingSolution.TotalUsageCount++;
                    
                    if (vote.IsUpvote)
                    {
                        existingSolution.SuccessCount++;
                    }

                    await _unitOfWork.Solutions.UpdateAsync(existingSolution);
                    solution = existingSolution;
                }
                else
                {
                    solution = new Solution
                    {
                        ProblemId = problem.Id,
                        ProblemDescription = problem.Description,
                        Category = problem.Category,
                        OperatingSystem = problem.OperatingSystem,
                        SolutionText = vote.SolutionText,
                        SuccessCount = vote.IsUpvote ? 1 : 0,
                        TotalUsageCount = 1,
                        CreatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Solutions.AddAsync(solution);
                }

                await _unitOfWork.SaveChangesAsync();

                // Vote kaydı oluştur
                var newVote = new Vote
                {
                    SolutionId = solution.Id,
                    IsUpvote = vote.IsUpvote,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Votes.AddAsync(newVote);
                await _unitOfWork.SaveChangesAsync();

                // Güncel oy sayılarını al
                var (upvotes, downvotes) = await GetVoteCountsAsync(solution.Id);

                return Json(new { 
                    success = true, 
                    message = vote.IsUpvote 
                        ? "Teşekkürler! Bu çözümü faydalı buldunuz." 
                        : "Teşekkürler! Geri bildiriminiz alındı.",
                    upvotes = upvotes,
                    downvotes = downvotes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveVote");
                return Json(new { success = false, message = $"Bir hata oluştu: {ex.Message}" });
            }
        }

        private async Task<(int upvotes, int downvotes)> GetVoteCountsAsync(int solutionId)
        {
            try
            {
                var votes = await _unitOfWork.Votes.FindListAsync(v => v.SolutionId == solutionId);
                
                if (votes == null)
                {
                    _logger.LogWarning($"No votes found for solution {solutionId}");
                    return (0, 0);
                }

                var votesList = votes.ToList();
                var upvotes = votesList.Count(v => v.IsUpvote);
                var downvotes = votesList.Count(v => !v.IsUpvote);

                _logger.LogInformation($"Vote counts for solution {solutionId}: Upvotes={upvotes}, Downvotes={downvotes}");
                
                return (upvotes, downvotes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting vote counts for solution {solutionId}");
                return (0, 0);
            }
        }

        // AI'ın başarılı çözümleri önceliklendirmesi için yeni metod
        [HttpGet]
        public async Task<IActionResult> GetSuccessfulSolutions(string category, string operatingSystem, int top = 5)
        {
            try
            {
                var query = await _unitOfWork.Solutions.GetQueryable();
                
                var successfulSolutions = await query
                    .Where(s => 
                        s.Category == category && 
                        s.OperatingSystem == operatingSystem && 
                        s.SuccessCount > 0)
                    .OrderByDescending(s => s.SuccessCount)
                    .Take(top)
                    .ToListAsync();

                return Json(new { 
                    success = true, 
                    solutions = successfulSolutions.Select(s => new {
                        problemDescription = s.ProblemDescription,
                        solutionText = s.SolutionText,
                        successRate = (double)s.SuccessCount / s.TotalUsageCount,
                        totalUses = s.TotalUsageCount
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting successful solutions");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
