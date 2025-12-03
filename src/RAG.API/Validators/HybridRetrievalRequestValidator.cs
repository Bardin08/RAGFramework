using FluentValidation;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.Core.Configuration;

namespace RAG.API.Validators;

/// <summary>
/// FluentValidation validator for HybridRetrievalRequest.
/// </summary>
public class HybridRetrievalRequestValidator : AbstractValidator<HybridRetrievalRequest>
{
    public HybridRetrievalRequestValidator(IOptions<ValidationSettings> settings)
    {
        var config = settings.Value;

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query cannot be empty")
            .MaximumLength(config.MaxQueryLength)
            .WithMessage($"Query cannot exceed {config.MaxQueryLength} characters");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, config.MaxHybridSearchLimit)
            .When(x => x.TopK.HasValue)
            .WithMessage($"TopK must be between 1 and {config.MaxHybridSearchLimit}");

        RuleFor(x => x.Alpha)
            .InclusiveBetween(0.0, 1.0)
            .When(x => x.Alpha.HasValue)
            .WithMessage("Alpha must be between 0.0 and 1.0");

        RuleFor(x => x.Beta)
            .InclusiveBetween(0.0, 1.0)
            .When(x => x.Beta.HasValue)
            .WithMessage("Beta must be between 0.0 and 1.0");

        RuleFor(x => x)
            .Must(HaveValidWeightSum)
            .When(x => x.Alpha.HasValue && x.Beta.HasValue)
            .WithMessage("Alpha + Beta must equal 1.0");
    }

    private static bool HaveValidWeightSum(HybridRetrievalRequest request)
    {
        if (!request.Alpha.HasValue || !request.Beta.HasValue) return true;
        var sum = request.Alpha.Value + request.Beta.Value;
        return Math.Abs(sum - 1.0) <= 0.001; // Allow small floating point tolerance
    }
}
