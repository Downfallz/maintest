using System;
using System.Linq;
using Xunit;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.Events;
using DA.Game.Domain2.Matches.Messages;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared;

namespace DA.Game.Domain2.Tests.Matches
{
    public class MatchTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = DateTime.UtcNow;
        }

        private sealed class FakeRandom : IRandom
        {
            private readonly int _value;
            public FakeRandom(int value) => _value = value;
            public int Next(int min, int max) => _value;
        }

        [Fact]
        public void Join_FirstPlayer_AssignsPlayer1_EmitsPlayerJoined()
        {
            var clock = new FakeClock();
            var rng = new FakeRandom(0);

            var match = new Match(MatchId.New());
            var player = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "player1");

            var res = match.Join(player, clock, rng);

            Assert.True(res.IsSuccess);
            Assert.Equal(MatchState.WaitingForPlayers, res.Value);
            Assert.NotNull(match.PlayerRef1);
            Assert.Equal(player.Id, match.PlayerRef1!.Id);
            Assert.Contains(match.DomainEvents, e => e is PlayerJoined);
        }

        [Fact]
        public void Join_SecondPlayer_StartsMatch_EmitsMatchStartedAndTurnAdvanced()
        {
            var clock = new FakeClock();
            // choose starting player deterministically (0 -> Player1)
            var rng = new FakeRandom(0);

            var match = new Match(MatchId.New());
            var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
            var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");

            var r1 = match.Join(p1, clock, rng);
            var r2 = match.Join(p2, clock, rng);

            Assert.True(r2.IsSuccess);
            Assert.Equal(MatchState.Started, match.State);
            Assert.NotNull(match.CurrentPlayerSlot);
            Assert.Equal(PlayerSlot.Player1, match.CurrentPlayerSlot);
            Assert.Equal(1, match.TurnNumber);

            Assert.Contains(match.DomainEvents, e => e is MatchStarted);
            Assert.Contains(match.DomainEvents, e => e is TurnAdvanced);
        }

        [Fact]
        public void Join_WhenAlreadyStarted_ReturnsAlreadyStartedError()
        {
            var clock = new FakeClock();
            var rng = new FakeRandom(0);

            var match = new Match(MatchId.New());
            var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
            var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");
            var p3 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p3");

            match.Join(p1, clock, rng);
            match.Join(p2, clock, rng);

            var r3 = match.Join(p3, clock, rng);

            Assert.False(r3.IsSuccess);
            Assert.Equal(MatchErrors.AlreadyStarted, r3.Error);
        }

        [Fact]
        public void EndTurn_Succeeds_WhenActorIsCurrentPlayer_TogglesSlotAndIncrementsTurn()
        {
            var clock = new FakeClock();
            // force starting player = Player1
            var rng = new FakeRandom(0);

            var match = new Match(MatchId.New());
            var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
            var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");

            match.Join(p1, clock, rng);
            match.Join(p2, clock, rng);

            // current should be Player1
            var actor = match.PlayerRef1!.Id;
            var beforeTurn = match.TurnNumber;
            var beforeSlot = match.CurrentPlayerSlot;

            var res = match.EndTurn(actor, clock);

            Assert.True(res.IsSuccess);
            Assert.Equal(beforeTurn + 1, match.TurnNumber);
            Assert.NotEqual(beforeSlot, match.CurrentPlayerSlot);
            Assert.Contains(match.DomainEvents, e => e is TurnAdvanced);
        }

        [Fact]
        public void EndTurn_Fails_WhenNotYourTurn()
        {
            var clock = new FakeClock();
            // force starting player = Player1
            var rng = new FakeRandom(0);

            var match = new Match(MatchId.New());
            var p1 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p1");
            var p2 = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "p2");

            match.Join(p1, clock, rng);
            match.Join(p2, clock, rng);

            // try to end turn with the player who is NOT current
            var wrongActor = match.CurrentPlayerSlot == PlayerSlot.Player1 ? match.PlayerRef2!.Id : match.PlayerRef1!.Id;
            var res = match.EndTurn(wrongActor, clock);

            Assert.False(res.IsSuccess);
            Assert.Equal(MatchErrors.NotYourTurn, res.Error);
        }

        [Fact]
        public void EndTurn_Fails_WhenMatchNotStarted()
        {
            var clock = new FakeClock();
            var rng = new FakeRandom(0);

            var match = new Match(MatchId.New());
            var actor = PlayerId.New();

            var res = match.EndTurn(actor, clock);

            Assert.False(res.IsSuccess);
            Assert.Equal(MatchErrors.NotStarted, res.Error);
        }
    }
}