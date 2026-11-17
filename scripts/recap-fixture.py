"""Rebuilds table/battle-recap.fixture.json, the recorded match the recap's tests read (docs/tabletop/async-recap-plan.md, R2).

    dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
    python3 scripts/recap-fixture.py match.trace.json data/dst/game.schema.json table/battle-recap.fixture.json

The trace is filtered as SeatVisibility filters seat player1's feed: the other seat's hidden decisions are dropped.
"""
import json, sys
d = json.load(open(sys.argv[1])); schema = json.load(open(sys.argv[2]))
PRIVATE = {'IntentSubmitted','SpeedChoiceSubmitted','EvolutionChoiceSubmitted','EvolutionPassed','TieOrderSubmitted'}
CKEYS = ['id','owner','name','health','maxHealth','energy','totalDefense','isAlive']
def creature(c): return {k: c[k] for k in CKEYS if k in c}
def event(ev):
    ev = {k: v for k, v in ev.items() if k not in ('matchId','player','player1','player2','contentHash')}
    if 'frame' in ev:
        slim = lambda c: {k: c[k] for k in ('id','health','maxHealth','energy','isAlive')}
        ev['frame'] = {'before': [slim(c) for c in ev['frame']['before']], 'after': [slim(c) for c in ev['frame']['after']]}
    if 'resolution' in ev:
        ev['resolution'] = {k: v for k, v in ev['resolution'].items() if k not in ('outcomes','effectiveTargets','criticalRoll','energySpent')}
    return ev
KEEP = {1,2,3,4,8,9,11,12,16,17}
feed = []
for e in d['entries']:
    ev = e['event']
    if ev['kind'] in PRIVATE and ev.get('slot') != 'Player1': continue
    rnd = ev.get('roundId', e['round'])
    if rnd is not None and rnd not in KEEP: continue
    feed.append({'sequence': e['sequence'], 'round': e['round'], 'subPhase': e['subPhase'], 'event': event(ev)})
BKEYS = ['slot','state','roundNumber','phase','subPhase','speedChoices','intents','evolutionChoices','timeline','outcome']
def board(b):
    out = {k: b[k] for k in BKEYS if k in b}
    out['allies'] = [creature(c) for c in b['allies']]; out['enemies'] = [creature(c) for c in b['enemies']]
    return out
by_seq = {e['sequence']: e for e in d['entries']}
def first(kind, rnd, slot='Player1'):
    return next(e['sequence'] for e in d['entries'] if e['event']['kind'] == kind and e['event'].get('roundId') == rnd and e['event'].get('slot') == slot)
points = {}
for r in (1, 2, 4, 9, 12):
    points[f'round{r}Tempo'] = first('SpeedChoiceSubmitted', r) - 1
points['round4Combat'] = next(e['sequence'] for e in d['entries'] if e['event']['kind'] == 'TimelineBuilt' and e['event']['roundId'] == 4)
points['end'] = d['entries'][-1]['sequence']
boards = {name: {'sequence': seq, 'board': board(by_seq[seq]['player1'])} for name, seq in points.items()}
used_spells = {e['event']['resolution']['action']['spell'] for e in feed if e['event']['kind'] == 'CombatActionResolved'}
cards = [{'id': s['id'], 'name': s['name']} for s in schema['spells'] if s['id'] in used_spells]
packages = [{'id': t['id'], 'name': t['name']} for t in schema['tiers']]
json.dump({'note': 'A recorded 3v3 match (play --seed 1, at-activation), as seat player1 reads it: the seat feed of rounds 1-4, 8-9, 11-12 and 16-17, frames trimmed to the stats the recap reads, and the board at a few named moments. Rebuilt by scripts/recap-fixture.py.',
           'catalogue': {'cards': cards, 'packages': packages}, 'feed': feed, 'boards': boards}, open(sys.argv[3], 'w'), separators=(',', ':'))
