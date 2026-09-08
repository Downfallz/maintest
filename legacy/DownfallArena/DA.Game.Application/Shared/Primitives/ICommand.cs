namespace DA.Game.Application.Shared.Primitives;

public interface ICommand<TRes> : MediatR.IRequest<TRes> { }