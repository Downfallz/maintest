namespace DA.Game.Application.Shared.Primitives;

public interface IQuery<TRes> : MediatR.IRequest<TRes> { }