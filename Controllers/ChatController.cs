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
                _logger.LogInformation($"Vote data received: {JsonConvert.SerializeObject(vote)}");

                if (vote == null)
                {
                    _logger.LogError("Vote DTO is null after deserialization");
                    return Json(new { success = false, message = "Geçersiz istek: Vote verisi okunamadı" });
                }

                // Önce aynı çözüm metni ile daha önce kaydedilmiş bir solution var mı kontrol et
                var existingSolution = await _unitOfWork.Solutions
                    .GetFirstOrDefaultAsync(s => 
                        s.SolutionText == vote.SolutionText && 
                        s.Category == vote.Category &&
                        s.OperatingSystem == vote.OperatingSystem);

                Solution solution;

                if (existingSolution != null)
                {
                    // Var olan solution'ı güncelle
                    _logger.LogInformation($"Updating existing solution with ID: {existingSolution.Id}");
                    
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
                    // Önce Problem kaydı oluştur
                    var problem = new Problem
                    {
                        Description = vote.ProblemDescription,
                        Category = vote.Category ?? "Genel",
                        OperatingSystem = vote.OperatingSystem ?? "Diğer",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Problems.AddAsync(problem);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation($"Created problem with ID: {problem.Id}");

                    // Problem'den referans alarak Solution kaydı oluştur
                    solution = new Solution
                    {
                        ProblemId = problem.Id,
                        ProblemDescription = problem.Description,  // Problem'den al
                        Category = problem.Category,              // Problem'den al
                        OperatingSystem = problem.OperatingSystem, // Problem'den al
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
    }
}
