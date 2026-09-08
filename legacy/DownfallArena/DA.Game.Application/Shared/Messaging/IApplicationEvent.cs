using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Shared.Messaging;

// Application : cross-BC / notification / intégration
public interface IApplicationEvent : IEvent, INotification { }