using FluentValidation;
using LombardAPI_NEW.Models.DTOs;

public class AddOrRemoveDLMembershipDtoValidator : AbstractValidator<AddOrRemoveDLMembershipDTO>
{
    //public AddOrRemoveDLMembershipDtoValidator()
    //{
    //    RuleFor(x => x.DLName)
    //        .NotEmpty().WithMessage("DL Name is required.")
    //        .MinimumLength(3).WithMessage("DL Name must be at least 3 characters long.");

    //    RuleFor(x => x.DLOwner)
    //        .NotEmpty().WithMessage("DL Owner is required.");

    //    RuleFor(x => x.Action)
    //        .NotEmpty().WithMessage("Action is required.")
    //        .Must(x => x.Equals("Add", StringComparison.OrdinalIgnoreCase) ||
    //                   x.Equals("Remove", StringComparison.OrdinalIgnoreCase))
    //        .WithMessage("Action must be either 'Add' or 'Remove'.");

    //    RuleFor(x => x.EmpIds)
    //        .NotEmpty().WithMessage("Employee/Consultant NT IDs are required.");
    //        //.Must(BeValidNTIDs).WithMessage("One or more NT IDs are invalid. Only alphanumeric values allowed, separated by commas.");
    //}

    private bool BeValidNTIDs(List<string> list)
    {
        throw new NotImplementedException();
    }

    private bool BeValidNTIDs(string? empIds)
    {
        if (string.IsNullOrWhiteSpace(empIds))
            return false;

        var ids = empIds.Split(',')
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x));

        return ids.All(id => id.All(char.IsLetterOrDigit));
    }
}
