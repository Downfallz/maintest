using DA.Game.Domain2.Players.Messages;
using FluentValidation;

namespace DA.Game.Application.Players.Features.Create;

public class CreatePlayerCommandValidator : AbstractValidator<CreatePlayerCommand>
{
    public CreatePlayerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(PlayerErrors.InvalidName);
    }
}
