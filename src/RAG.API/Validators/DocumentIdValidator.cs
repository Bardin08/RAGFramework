using FluentValidation;

namespace RAG.API.Validators;

/// <summary>
/// FluentValidation validator for document ID parameters.
/// </summary>
public class DocumentIdValidator : AbstractValidator<DocumentIdRequest>
{
    public DocumentIdValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Document ID is required")
            .Must(BeValidGuid)
            .WithMessage("Document ID must be a valid GUID format");
    }

    private static bool BeValidGuid(Guid id)
    {
        return id != Guid.Empty;
    }
}

/// <summary>
/// Request wrapper for document ID validation.
/// </summary>
public class DocumentIdRequest
{
    public Guid Id { get; set; }
}
