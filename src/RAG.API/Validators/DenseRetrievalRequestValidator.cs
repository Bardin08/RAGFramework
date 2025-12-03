using FluentValidation;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.Core.Configuration;

namespace RAG.API.Validators;

/// <summary>
/// FluentValidation validator for DenseRetrievalRequest.
/// </summary>
public class DenseRetrievalRequestValidator : AbstractValidator<DenseRetrievalRequest>
{
    public DenseRetrievalRequestValidator(IOptions<ValidationSettings> settings)
    {
        var config = settings.Value;

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query cannot be empty")
            .MaximumLength(config.MaxQueryLength)
            .WithMessage($"Query cannot exceed {config.MaxQueryLength} characters");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, config.MaxTopK)
            .When(x => x.TopK.HasValue)
            .WithMessage($"TopK must be between 1 and {config.MaxTopK}");

        RuleFor(x => x.Threshold)
            .InclusiveBetween(0.0, 1.0)
            .When(x => x.Threshold.HasValue)
            .WithMessage("Threshold must be between 0.0 and 1.0");
    }
}
