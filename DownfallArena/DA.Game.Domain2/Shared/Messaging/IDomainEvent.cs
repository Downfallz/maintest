using DA.Game.Shared;
using MediatR;

namespace DA.Game.Domain2.Shared.Messaging;

public interface IDomainEvent : IEvent, INotification
{
}
