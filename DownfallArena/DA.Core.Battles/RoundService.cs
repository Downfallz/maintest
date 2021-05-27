using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DA.Core.Battles.Abstractions;
using DA.Core.Battles.Mechanic.Abstractions;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Base.Teams.Enum;
using DA.Core.Domain.Battles;
using DA.Core.Domain.Battles.Enum;
using DA.Core.Teams;
using DA.Core.Teams.Abstractions;

namespace DA.Core.Battles
{
    public class RoundService : IRoundService
    {
        private readonly IAppliedEffectManager _appliedEffectService;
        private readonly ICharacterCondManager _characterCondService;
        private readonly ICharacterDevelopmentService _characterDevelopmentService;
        private readonly IPlayerActionHandler _spellService;

        public RoundService(IAppliedEffectManager appliedEffectService, ICharacterCondManager characterCondService, ICharacterDevelopmentService characterDevelopmentService, IPlayerActionHandler spellService)
        {
            _appliedEffectService = appliedEffectService;
            _characterCondService = characterCondService;
            _characterDevelopmentService = characterDevelopmentService;
            _spellService = spellService;
        }
        public void InitializeNewRound(Battle battle)
        {
            if (battle.BattleStatus != BattleStatus.Started)
                throw new Exception("Invalid status to be initializing a new round.");

            if (battle.CurrentRound == null)
            {
                InitializeRound(battle);
            }
            else
            {
                if (battle.CurrentRound.RoundStatus != RoundStatus.Finished)
                    throw new Exception("Can't initialize a new round if current is not finished.");

                InitializeRound(battle);
            }
        }

        private void InitializeRound(Battle battle)
        {
            if (battle.CurrentRound != null)
            {
                battle.FinishedRoundsHistory.Add(battle.CurrentRound);
            }

            battle.CurrentRound = new Round();
            battle.CurrentRound.RoundStatus = RoundStatus.Created;

            ResolveRoundStart(battle);
        }

        private void ResolveRoundStart(Battle battle)
        {
            AppliedEffect roundStartEnergy = new AppliedEffect();
            roundStartEnergy.EffectType = Domain.Base.Talents.Enum.EffectType.Temporary;
            roundStartEnergy.Length = 1;
            roundStartEnergy.StatModifier = new Domain.Base.StatModifier() { Modifier = 2, StatType = Domain.Base.Talents.Enum.Stats.Energy };


            _appliedEffectService.ApplyEffect(roundStartEnergy, null, battle.AllAliveCharacters);


            foreach (Character c in battle.AllAliveCharacters)
            {
                foreach (CharCondition cc in c.CharConditions)
                {
                    _characterCondService.ApplyCondition(cc, c);
                }

                c.CharConditions.RemoveAll(x => x.RoundsLeft <= 0);
            }
        }

        public bool ChooseSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices)
        {
            if (ti == TeamIndicator.One)
            {
                battle.CurrentRound.PlayerOneSpellUnlocks = choices;
            }
            else
            {
                battle.CurrentRound.PlayerTwoSpellUnlocks = choices;
            }

            if (battle.CurrentRound.PlayerOneSpellUnlocks.Count > 0 && battle.CurrentRound.PlayerTwoSpellUnlocks.Count > 0)
            {
                return true;
            }

            return false;
        }

        public void ApplySpellsToUnlock(Battle battle)
        {
            foreach (var choice in battle.CurrentRound.PlayerOneSpellUnlocks)
            {
                var c = battle.AllCharacter.Single(x => x.Id == choice.CharacterId);
                _characterDevelopmentService.UnlockSpell(c, choice.Spell);
            }
            foreach (var choice in battle.CurrentRound.PlayerTwoSpellUnlocks)
            {
                var c = battle.AllCharacter.Single(x => x.Id == choice.CharacterId);
                _characterDevelopmentService.UnlockSpell(c, choice.Spell);
            }
        }

        public bool ChooseSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices)
        {
            if (ti == TeamIndicator.One)
            {
                battle.CurrentRound.PlayerOneSpeedChoice = choices;
            }
            else
            {
                battle.CurrentRound.PlayerTwoSpeedChoice = choices;
            }

            if (battle.CurrentRound.PlayerOneSpeedChoice.Count > 0 && battle.CurrentRound.PlayerTwoSpeedChoice.Count > 0)
            {
                return true;
            }

            return false;
        }

        public void ResolveCharacterOrder(Battle battle)
        {
            var round = battle.CurrentRound;
            var quickCharacter = round.PlayerOneSpeedChoice.Where(x => x.Speed == Speed.Quick).ToList();
            quickCharacter.AddRange(round.PlayerTwoSpeedChoice.Where(x => x.Speed == Speed.Quick).ToList());

            var normalCharacters = round.PlayerOneSpeedChoice.Where(x => x.Speed == Speed.Standard).ToList();
            normalCharacters.AddRange(round.PlayerTwoSpeedChoice.Where(x => x.Speed == Speed.Standard).ToList());


            var listQuick = quickCharacter.Select(x => battle.AllCharacter.Single(y => y.Id == x.CharacterId)).ToList();
            var listNormal = normalCharacters.Select(x => battle.AllCharacter.Single(y => y.Id == x.CharacterId)).ToList();
            foreach (var choice in listQuick.OrderByDescending(x => x.Initiative))
            {
                round.OrderedCharacters.Add(choice);
            }
            foreach (var choice in listNormal.OrderByDescending(x => x.Initiative))
            {
                round.OrderedCharacters.Add(choice);
            }
            round.CurrentCharacterIndex = 0;
            round.RoundStatus = RoundStatus.Playing;
        }

        public Guid? GetCurrentCharacterId(Round round)
        {
            if (round.CurrentCharacterIndex.HasValue)
            {
                return round.OrderedCharacters[round.CurrentCharacterIndex.Value].Id;
            }
            return null;
        }

        public void PlayAndResolveCharacterAction(Round round, CharacterActionChoice characterActionChoice)
        {
            // play
            var targetSpeed = round.AllSpeedChoice.Single(x => x.CharacterId.Equals(characterActionChoice.CharacterId));

            var sourceChar = round.OrderedCharacters.Single(x => x.Id == characterActionChoice.CharacterId);

            var listeTargets = new List<Character>();
            foreach (var cId in characterActionChoice.Targets)
            {
                listeTargets.Add(round.OrderedCharacters.Single(x => x.Id == cId));
            }
            _spellService.PlaySpell(sourceChar, characterActionChoice.Spell, listeTargets, targetSpeed.Speed);
            AssignNextCharacter(round);

        }

        private static void AssignNextCharacter(Round round)
        {
            if (round.CurrentCharacterIndex.Value == round.OrderedCharacters.Count - 1)
            {
                round.CurrentCharacterIndex = null;
            }
            else
            {
                for (var i = round.CurrentCharacterIndex.Value + 1; i < round.OrderedCharacters.Count; i++)
                {
                    if (!round.OrderedCharacters[i].IsDead)
                    {
                        round.CurrentCharacterIndex = i;
                        break;
                    }

                    if (i == round.OrderedCharacters.Count - 1)
                    {
                        if (round.OrderedCharacters[i].IsDead)
                        {
                            round.CurrentCharacterIndex = null;
                            break;
                        }
                    }
                }
            }

            if (!round.CurrentCharacterIndex.HasValue)
            {
                round.RoundStatus = RoundStatus.Finished;
            }
        }
    }
}
