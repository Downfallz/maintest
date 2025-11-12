Round/

Round_Turn, Round_Phase, Round_Timeouts, Round_StreakNetDmg

MyTeam/ (slots P1/P2/P3 → suffixes S1,S2,S3)

My_S1_HP, My_S1_HPpct, My_S1_Energy, My_S1_IsStunned

My_S1_T_Brawler_Pummel (talents bool 0/1)

Idem S2/S3

OppTeam/ (mêmes colonnes que My mais côté adverse)

Opp_S1_HPpct, Opp_S2_IsShielded, talents adverses si connus/observables

Context/ (état global)

Ctx_ActiveBuffsCount, Ctx_Weather, Ctx_FieldModifier, etc.

History/ (lag features → petit historique 1–3 tours)

Hist_LastMyDmg, Hist_LastOppDmg, Hist_LastMyHeal, Hist_LastActionKey (caté courte)

Resources/

Res_MyManaAvg, Res_OppManaAvg, Res_Cooldowns_My_Fireball (0/1 si prêt)

PolicyHints/ (facultatif, heuristiques maison)

Hint_IsFinisherOpportunity, Hint_ShouldProtectHealer

Decision/ (labels/outputs)

soit décomposé : Dec_Init_P1/P2/P3, Dec_Sel1/2, Dec_Spell_Sel1/2

soit composite : Dec_Key = "I=Q,S,Q|S=1,3|A=FIREBALL,HEAL"

Reward : Dec_RewardRound (numérique)

Avec 3 persos/équipe, talents en bool, et quelques lags, tu arrives ~200–300 colonnes sans souci.