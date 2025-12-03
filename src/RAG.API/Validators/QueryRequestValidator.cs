using FluentValidation;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.Core.Configuration;

namespace RAG.API.Validators;

/// <summary>
/// FluentValidation validator for QueryRequest.
/// </summary>
public class QueryRequestValidator : AbstractValidator<QueryRequest>
{
    public QueryRequestValidator(IOptions<ValidationSettings> settings)
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

        RuleFor(x => x.Strategy)
            .Must(BeValidStrategy)
            .When(x => !string.IsNullOrWhiteSpace(x.Strategy))
            .WithMessage("Strategy must be one of: BM25, Dense, Hybrid, Adaptive");

        RuleFor(x => x.Provider)
            .Must(BeValidProvider)
            .When(x => !string.IsNullOrWhiteSpace(x.Provider))
            .WithMessage("Provider must be one of: OpenAI, Ollama");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0, 1.0)
            .When(x => x.Temperature.HasValue)
            .WithMessage("Temperature must be between 0.0 and 1.0");

        RuleFor(x => x.MaxTokens)
            .InclusiveBetween(1, 4000)
            .When(x => x.MaxTokens.HasValue)
            .WithMessage("MaxTokens must be between 1 and 4000");
    }

    private static bool BeValidStrategy(string? strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy)) return true;
        var validStrategies = new[] { "BM25", "Dense", "Hybrid", "Adaptive" };
        return validStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase);
    }

    private static bool BeValidProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return true;
        var validProviders = new[] { "OpenAI", "Ollama" };
        return validProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);
    }
}
