using FluentValidation;
using Microsoft.Extensions.Options;
using RAG.API.Models.Requests;
using RAG.Core.Configuration;

namespace RAG.API.Validators;

/// <summary>
/// FluentValidation validator for DocumentUploadRequest.
/// </summary>
public class DocumentUploadRequestValidator : AbstractValidator<DocumentUploadRequest>
{
    private readonly ValidationSettings _settings;

    public DocumentUploadRequestValidator(IOptions<ValidationSettings> settings)
    {
        _settings = settings.Value;

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required")
            .Must(BeValidFileSize)
            .WithMessage($"File size cannot exceed {_settings.MaxFileSizeMb}MB")
            .Must(BeValidFileExtension)
            .WithMessage($"Allowed file types: {string.Join(", ", _settings.AllowedFileExtensions)}");

        RuleFor(x => x.Title)
            .MaximumLength(_settings.MaxTitleLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Title))
            .WithMessage($"Title cannot exceed {_settings.MaxTitleLength} characters");

        RuleFor(x => x.Source)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Source))
            .WithMessage("Source cannot exceed 1000 characters");
    }

    private bool BeValidFileSize(IFormFile? file)
    {
        if (file == null) return false;
        return file.Length <= _settings.MaxFileSizeBytes;
    }

    private bool BeValidFileExtension(IFormFile? file)
    {
        if (file == null) return false;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return _settings.AllowedFileExtensions
            .Select(e => e.ToLowerInvariant())
            .Contains(extension);
    }
}
