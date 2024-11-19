using System;

namespace Fixer_Web.Models.ViewModels
{
    public class SolutionViewModel
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public bool IsAIGenerated { get; set; }
        public int SuccessRate { get; set; }
        public int VoteCount { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class SolutionCreateViewModel
    {
        public int ProblemId { get; set; }
        public string Content { get; set; }
    }
} 