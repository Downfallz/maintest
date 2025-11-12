using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Shared.Messaging;

// Application : cross-BC / notification / intégration
public interface IApplicationEvent : IEvent, INotification { }