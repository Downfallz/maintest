using DA.Game.Application.Players.Features.Create;
using DA.Game.Application.Players.Features.Create.Notifications;
using DA.Game.Application.Players.Ports;
using DA.Game.Application.Shared.Abstractions;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Players.Enums;
using DA.Game.Domain2.Players.Messages;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared;
using Moq;

public class CreatePlayerHandlerTests
{
    private readonly Mock<IPlayerRepository> _repo = new();
    private readonly Mock<IPlayerUniqueness> _unique = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IEventBus> _bus = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IApplicationEventCollector> _appEvents = new();

    private CreatePlayerHandler CreateHandler() =>
        new(_repo.Object, _unique.Object, _appEvents.Object, _clock.Object);

    [Fact]
    public async Task HandleAsync_ReturnsFail_WhenNameIsNullOrWhitespace()
    {
        var handler = CreateHandler();
        var cmd = new CreatePlayerCommand(null, ActorKind.Human);

        var result = await handler.Handle(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayerErrors.InvalidName, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_ReturnsFail_WhenNameIsEmptyOrWhitespace(string name)
    {
        var handler = CreateHandler();
        var cmd = new CreatePlayerCommand(name, ActorKind.Bot);

        var result = await handler.Handle(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayerErrors.InvalidName, result.Error);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFail_WhenNameAlreadyTaken()
    {
        var handler = CreateHandler();
        var cmd = new CreatePlayerCommand("TakenName", ActorKind.Human);

        _unique.Setup(u => u.ExistsNameAsync("TakenName", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await handler.Handle(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayerErrors.NameAlreadyTaken, result.Error);
    }

    [Fact]
    public async Task HandleAsync_CreatesPlayerAndPublishesEvent_WhenNameIsValidAndUnique()
    {
        var handler = CreateHandler();
        var cmd = new CreatePlayerCommand("ValidName", ActorKind.Bot);

        _unique.Setup(u => u.ExistsNameAsync("ValidName", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Player? savedPlayer = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()))
            .Callback<Player, CancellationToken>((p, _) => savedPlayer = p)
            .Returns(Task.CompletedTask);

        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        PlayerCreated? publishedEvent = null;
        _bus.Setup(b => b.PublishAsync(It.IsAny<PlayerCreated>(), It.IsAny<CancellationToken>()))
            .Callback<IEvent, CancellationToken>((evt, _) => publishedEvent = evt as PlayerCreated)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(cmd);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(savedPlayer);
        Assert.Equal("ValidName", savedPlayer.Name);
        Assert.Equal(ActorKind.Bot, savedPlayer.Kind);

        Assert.NotNull(publishedEvent);
        Assert.Equal(savedPlayer.Id, publishedEvent.PlayerId);
        Assert.Equal("ValidName", publishedEvent.Name);
        Assert.True((DateTime.UtcNow - publishedEvent.OccurredAt).TotalSeconds < 5);
    }
}