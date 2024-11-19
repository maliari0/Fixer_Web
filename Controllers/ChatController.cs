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
                _logger.LogInformation($"Received vote request: {JsonSerializer.Serialize(vote)}");

                var existingVote = await _unitOfWork.Votes.FindOneAsync(v => 
                    v.SolutionId == vote.SolutionIndex);

                if (existingVote != null)
                {
                    if (existingVote.IsUpvote == vote.IsUpvote)
                    {
                        return Json(new { success = false, message = "Bu çözüm için zaten oy kullanmışsınız." });
                    }

                    existingVote.IsUpvote = vote.IsUpvote;
                    _unitOfWork.Votes.Update(existingVote);
                }
                else
                {
                    var newVote = new Vote
                    {
                        SolutionId = vote.SolutionIndex,
                        IsUpvote = vote.IsUpvote,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Votes.AddAsync(newVote);
                }

                await _unitOfWork.SaveChangesAsync();

                var voteCounts = await GetVoteCountsAsync(vote.SolutionIndex);

                return Json(new { 
                    success = true, 
                    message = vote.IsUpvote ? "Teşekkürler! Çözümü beğendiniz." : "Teşekkürler! Geri bildiriminiz alındı.",
                    upvotes = voteCounts.upvotes,
                    downvotes = voteCounts.downvotes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving vote");
                return Json(new { success = false, message = "Bir hata oluştu: " + ex.Message });
            }
        }

        private async Task<(int upvotes, int downvotes)> GetVoteCountsAsync(int solutionId)
        {
            var votes = await _unitOfWork.Votes.FindListAsync(v => v.SolutionId == solutionId);

            return (
                upvotes: votes.Count(v => v.IsUpvote),
                downvotes: votes.Count(v => !v.IsUpvote)
            );
        }
    }
}
