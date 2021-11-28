using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.CombatMechanic.Enum;
using DA.Game.Domain.Models.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services;
using DA.Game.Domain.Services.CombatMechanic;
using DA.Game.Domain.Services.TalentsManagement;

namespace DA.Game.CombatMechanic
{
    public class RoundService : IRoundService
    {
        private readonly IAppliedEffectService _appliedEffectService;
        private readonly ICharacterCondService _characterCondService;
        private readonly ICharacterDevelopmentService _characterDevelopmentService;
        private readonly ISpellResolverService _spellService;
        private readonly IGameLogger _gameLogger;

        public RoundService(IAppliedEffectService appliedEffectService, ICharacterCondService characterCondService, ICharacterDevelopmentService characterDevelopmentService, ISpellResolverService spellService, IGameLogger gameLogger)
        {
            _appliedEffectService = appliedEffectService;
            _characterCondService = characterCondService;
            _characterDevelopmentService = characterDevelopmentService;
            _spellService = spellService;
            _gameLogger = gameLogger;
        }

        public void InitializeNewRound(Battle battle)
        {
            if (battle.BattleStatus != BattleStatus.Started)
                throw new Exception("Invalid status to be initializing a new round.");

            if (battle.CurrentRound != null)
            {
                if (battle.CurrentRound.RoundStatus != RoundStatus.Finished)
                    throw new Exception("Can't initialize a new round if current is not finished.");
            }
            InitializeRound(battle);
        }

        private void InitializeRound(Battle battle)
        {
            battle.CurrentRound = new Round
            {
                RoundStatus = RoundStatus.Created,
                RoundNumber = battle.FinishedRoundsHistory.Count + 1
            };

           ResolveRoundStart(battle);
        }

        private void ResolveRoundStart(Battle battle)
        {
            AppliedEffect roundStartEnergy = new AppliedEffect
            {
                EffectType = EffectType.Temporary,
                Length = 1,
                StatModifier = new StatModifier() { Modifier = 2, StatType = Stats.Energy }
            };

            _appliedEffectService.ApplyEffect(roundStartEnergy, null, battle.AllAliveCharacters);
            List<CharCondApplyResult> charCondApplyResults = new List<CharCondApplyResult>();

            foreach (Character c in battle.AllAliveCharacters)
            {
                foreach (CharCondition cc in c.CharConditions)
                {
                    if (!c.IsDead)
                    {
                        charCondApplyResults.Add(_characterCondService.ApplyCondition(cc, c));
                    }
                }

                c.CharConditions.RemoveAll(x => x.RoundsLeft <= 0);
            }

            battle.CurrentRound.RoundStartConditions = charCondApplyResults;
        }

        public void ApplySpellsToUnlock(Battle battle)
        {
            foreach (SpellUnlockChoice choice in battle.CurrentRound.PlayerOneSpellUnlocks)
            {
                Character c = battle.AllCharacter.Single(x => x.Id == choice.CharacterId);
                _characterDevelopmentService.UnlockSpell(c, choice.Spell);
            }
            foreach (SpellUnlockChoice choice in battle.CurrentRound.PlayerTwoSpellUnlocks)
            {
                Character c = battle.AllCharacter.Single(x => x.Id == choice.CharacterId);
                _characterDevelopmentService.UnlockSpell(c, choice.Spell);
            }
        }

        public void ResolveCharacterOrder(Battle battle)
        {
            Round round = battle.CurrentRound;
            List<SpeedChoice> quickCharacter = round.PlayerOneSpeedChoice.Where(x => x.Speed == Speed.Quick).ToList();
            quickCharacter.AddRange(round.PlayerTwoSpeedChoice.Where(x => x.Speed == Speed.Quick).ToList());

            List<SpeedChoice> normalCharacters = round.PlayerOneSpeedChoice.Where(x => x.Speed == Speed.Standard).ToList();
            normalCharacters.AddRange(round.PlayerTwoSpeedChoice.Where(x => x.Speed == Speed.Standard).ToList());


            List<Character> listQuick = quickCharacter.Select(x => battle.AllCharacter.Single(y => y.Id == x.CharacterId)).ToList();
            List<Character> listNormal = normalCharacters.Select(x => battle.AllCharacter.Single(y => y.Id == x.CharacterId)).ToList();
            foreach (Character choice in listQuick.OrderByDescending(x => x.Initiative))
            {
                round.OrderedCharacters.Add(choice);
            }
            foreach (Character choice in listNormal.OrderByDescending(x => x.Initiative))
            {
                round.OrderedCharacters.Add(choice);
            }
            round.CurrentCharacterIndex = 0;
            round.RoundStatus = RoundStatus.Playing;
        }

        public Guid? GetCurrentCharacterIdActionTurn(Round round)
        {
            if (round.CurrentCharacterIndex.HasValue)
            {
                return round.OrderedCharacters[round.CurrentCharacterIndex.Value].Id;
            }
            return null;
        }


        public void AssignNextCharacter(Round round)
        {
            if (round.CurrentCharacterIndex.Value == round.OrderedCharacters.Count - 1)
            {
                round.CurrentCharacterIndex = null;
            }
            else
            {
                for (int i = round.CurrentCharacterIndex.Value + 1; i < round.OrderedCharacters.Count; i++)
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


        public bool ChooseTeamSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices)
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

        public bool ChooseTeamSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices)
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

        public void PlayAndResolveCharacterAction(Round round, CharacterActionChoice characterActionChoice)
        {
            if (round == null)
                throw new ArgumentNullException(nameof(round));

            if (round.RoundStatus != RoundStatus.Playing)
                throw new System.Exception("Can't play a character choice if round is not in status playing");

            if (characterActionChoice != null)
            {
                Character sourceChar = round.OrderedCharacters.Single(x => x.Id == characterActionChoice.CharacterId);

                if (sourceChar.IsDead)
                    throw new System.Exception("Can't play a character choice if source character is dead.");
                if (sourceChar.IsStunned)
                    throw new System.Exception("Can't play a character choice if source character is stunned.");
                round.CharacterActionChoices.Add(characterActionChoice);

                // play
                SpeedChoice targetSpeed = round.AllSpeedChoice.Single(x => x.CharacterId.Equals(characterActionChoice.CharacterId));
                if (targetSpeed == null)
                    throw new Exception("Can't find target speed for action");

                List<Character> listeTargets = new List<Character>();
                foreach (Guid cId in characterActionChoice.Targets)
                {
                    var targetChar = round.OrderedCharacters.Single(x => x.Id == cId);
                    if (targetChar.IsDead)
                        throw new System.Exception("Target is dead.");
                    listeTargets.Add(targetChar);
                }

                var result = _spellService.PlaySpell(sourceChar, characterActionChoice.Spell, listeTargets, targetSpeed.Speed);
                _gameLogger.Log($"{result.SourceCharInfo.Name} (Team {result.SourceCharInfo.Team}) played {result.Spell.Name} on {result.TargetsCharInfo.Select(x => x.Name)}");
            }
        }
    }
}
