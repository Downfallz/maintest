# host-reviewer lessons, #862/#856/#860/#861/#833 PR

- A token check fixed by grep for `string.Equals` misses `==`: TableRegistry.ByTokenAsync (`seat.Token == token` after `TableKeys.TableOf`, the table id being public), `seat.Invitation == invitation`, `seat.Code == code`; SecretComparisonTests' regex matches only `string.Equals` (#862 PR).
- JournaledStepper's Journal.Gone (#856) keys on `kept.Position` Mark > 0, which LetGoAsync and a Moved refusal null: a held table whose last write was refused, or still at Mark 0, rebuilds from the empty journal and its next append at Mark 0 recreates the deleted run (both stores create at Mark 0) (#856 PR).
- ScenarioSources.HeldBackAsync (#833) reads one record per listed scenario on every `GET /api/scenarios` (each signed-in home page) and catches no store failure: a blob outage 500s the whole Practice list; check every guard added to a player-facing list for reads per row and a StoreFails catch (#833 PR).
