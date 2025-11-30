using FluentValidation;
using RAG.Core.Domain;

namespace RAG.Core.Validators;

/// <summary>
/// Validator for GenerationRequest ensuring all inputs meet requirements.
/// </summary>
public class GenerationRequestValidator : AbstractValidator<GenerationRequest>
{
    public GenerationRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query cannot be empty");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0)
            .WithMessage("MaxTokens must be greater than 0");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0m, 1.0m)
            .WithMessage("Temperature must be between 0.0 and 1.0");
    }
}
