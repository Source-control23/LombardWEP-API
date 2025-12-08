using FluentValidation;
using LombardAPI_NEW.Models.DTOs;


namespace LombardAPI_NEW.Validators
{
    public class AddOrRemoveRightsDTOValidator : AbstractValidator<AddOrRemoveRightsDTO>
    {
        public AddOrRemoveRightsDTOValidator()
        {
    //        RuleFor(x => x.DLName)
    //       .NotEmpty().WithMessage("DL Name is required.")
    //       .MinimumLength(3).WithMessage("DL Name must be at least 3 characters long.");

    //        RuleFor(x => x.DLOwner)
    //            .NotEmpty().WithMessage("DL Owner is required.");

    //        RuleFor(x => x.Action)
    //.NotEmpty().WithMessage("Action is required.")
    //.Must(action => action!.Equals("Add", StringComparison.OrdinalIgnoreCase)
    //             || action!.Equals("Remove", StringComparison.OrdinalIgnoreCase))
    //    .When(x => !string.IsNullOrEmpty(x.Action))
    //.WithMessage("Action must be either 'Add' or 'Remove'.");


    //        RuleFor(x => x.Rights)
    //            .NotEmpty().WithMessage("Rights are required.");


        }
    }
}



