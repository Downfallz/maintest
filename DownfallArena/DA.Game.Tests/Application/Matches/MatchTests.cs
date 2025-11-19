//using DA.Game.Domain2.Match.Enums;
//using DA.Game.Domain2.Match.Events;
//using DA.Game.Domain2.Match.ValueObjects;
//using DA.Game.Domain2.Matches.Ids;
//using DA.Game.Domain2.Matches.Messages;
//using DA.Game.Domain2.Players.Enums;
//using DA.Game.Domain2.Players.Ids;
//using DA.Game.Shared;
//using DA.Game.Tests.Support;
//using FluentAssertions;

//namespace DA.Game.Tests.Application.Matches;

//public class MatchTests
//{
//    private readonly TestFixture _fx = new();
//    private readonly IClock _clock;
//    private readonly IRandom _rng;

//    public MatchTests()
//    {
//        _clock = _fx.Get<IClock>();
//        _rng = _fx.Get<IRandom>();
//    }
//    [Fact]
//    public void Ctor_Succeeds_StateWaitingForPLayer()
//    {
//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());

//        match.State.Should().Be(MatchState.WaitingForPlayers);
//    }

//    [Fact]
//    public void Join_FirstPlayer_AssignsPlayer1_EmitsPlayerJoined()
//    {
//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());
//        var player = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "player1");

//        var res = match.Join(player, _fx.Get<IClock>(), _fx.Get<IRandom>());

//        Assert.True(res.IsSuccess);
//        Assert.Equal(MatchState.WaitingForPlayers, res.Value);
//        Assert.NotNull(match.PlayerRef1);
//        Assert.Equal(player.Id, match.PlayerRef1!.Id);
//        Assert.Contains(match.DomainEvents, e => e is PlayerJoined);
//    }

//    [Fact]
//    public void Join_SecondPlayer_StartsMatch_EmitsMatchStartedAndTurnAdvanced()
//    {
//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());
//        var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
//        var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");

//        var r1 = match.Join(p1, _clock, _rng);
//        var r2 = match.Join(p2, _clock, _rng);

//        Assert.True(r2.IsSuccess);
//        Assert.Equal(MatchState.Started, match.State);
//        Assert.NotNull(match.CurrentPlayerSlot);
//        Assert.Equal(PlayerSlot.Player1, match.CurrentPlayerSlot);
//        Assert.Equal(1, match.RoundNumber);

//        Assert.Contains(match.DomainEvents, e => e is MatchStarted);
//        Assert.Contains(match.DomainEvents, e => e is TurnAdvanced);
//    }

//    [Fact]
//    public void Join_WhenAlreadyStarted_ReturnsAlreadyStartedError()
//    {
//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());
//        var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
//        var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");
//        var p3 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p3");

//        match.Join(p1, _clock, _rng);
//        match.Join(p2, _clock, _rng);

//        var r3 = match.Join(p3, _clock, _rng);

//        Assert.False(r3.IsSuccess);
//        Assert.Equal(MatchErrors.AlreadyStarted, r3.Error);
//    }

//    [Fact]
//    public void EndTurn_Succeeds_WhenActorIsCurrentPlayer_TogglesSlotAndIncrementsTurn()
//    {
//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());
//        var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
//        var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");

//        match.Join(p1, _clock, _rng);
//        match.Join(p2, _clock, _rng);

//        // current should be Player1
//        var actor = match.PlayerRef1!.Id;
//        var beforeTurn = match.RoundNumber;
//        var beforeSlot = match.CurrentPlayerSlot;

//        var res = match.EndTurn(actor, _clock);

//        Assert.True(res.IsSuccess);
//        Assert.Equal(beforeTurn + 1, match.RoundNumber);
//        Assert.NotEqual(beforeSlot, match.CurrentPlayerSlot);
//        Assert.Contains(match.DomainEvents, e => e is TurnAdvanced);
//    }



//    [Fact]
//    public void EndTurn_Fails_WhenNotYourTurn()
//    {
//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());
//        var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
//        var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");

//        match.Join(p1, _clock, _rng);
//        match.Join(p2, _clock, _rng);

//        // try to end turn with the player who is NOT current
//        var wrongActor = match.CurrentPlayerSlot == PlayerSlot.Player1 ? match.PlayerRef2!.Id : match.PlayerRef1!.Id;
//        var res = match.EndTurn(wrongActor, _clock);

//        Assert.False(res.IsSuccess);
//        Assert.Equal(MatchErrors.NotYourTurn, res.Error);
//    }

//    [Fact]
//    public void EndTurn_Fails_WhenMatchNotStarted()
//    {

//        var match = new Domain2.Matches.Aggregates.Match(MatchId.New());
//        var actor = PlayerId.New();

//        var res = match.EndTurn(actor, _clock);

//        Assert.False(res.IsSuccess);
//        Assert.Equal(MatchErrors.NotStarted, res.Error);
//    }
//}