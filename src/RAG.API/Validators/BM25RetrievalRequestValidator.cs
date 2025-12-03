using FluentValidation;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.Core.Configuration;

namespace RAG.API.Validators;

/// <summary>
/// FluentValidation validator for BM25RetrievalRequest.
/// </summary>
public class BM25RetrievalRequestValidator : AbstractValidator<BM25RetrievalRequest>
{
    public BM25RetrievalRequestValidator(IOptions<ValidationSettings> settings)
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
    }
}
