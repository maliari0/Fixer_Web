using System;
using System.Collections.Generic;

namespace Fixer_Web.Models.ViewModels
{
    public class ProblemCreateViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string OperatingSystem { get; set; }
    }

    public class ProblemDetailViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public int ViewCount { get; set; }
        public List<SolutionViewModel> Solutions { get; set; }
    }

    public class ProblemListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public int SolutionCount { get; set; }
    }
} 