using AutoMapper;
using Fixer_Common.Models;
using Fixer_Web.Models.ViewModels;

namespace Fixer_Web.Infrastructure
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Problem, ProblemListViewModel>()
                .ForMember(dest => dest.SolutionCount,
                    opt => opt.MapFrom(src => src.Solutions.Count));

            CreateMap<Problem, ProblemDetailViewModel>();
            CreateMap<ProblemCreateViewModel, Problem>();

            CreateMap<Solution, SolutionViewModel>();
            CreateMap<SolutionCreateViewModel, Solution>();
        }
    }
} 