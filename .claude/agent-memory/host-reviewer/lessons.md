# host-reviewer memory

- RepositoryScenarios is read by the API process alone at startup; a bad file is dropped with a console line, never a crash. LessonReach runs only in tests and scenario-check, not at the host (O2, tutorial block).
- ScenariosApi.Hidden (ScenariosApi.cs:80 since #646, was :45) opens a shipped scenario (sc-repo-*, tutorials included) to any signed-in account when `--admins` is empty; recheck whenever shipped scenarios gain a new kind (O2).
- Content-only diffs under scenarios/ need no route test, but a tutorial outside scenarios/tutorials/ still counts in `Tutorials`: check the directory, not just the block (O2).
- A seat's asking (`JournaledStepper.Asking`) moves only on that seat's own decisions: a Late from DecideAsync on a seat answered in the background means someone else answered it, so the state moved; never drop the re-look on Late (O3a, TutorialPlay.AnswerAsync).
- Practices (ScenarioPractices) live in the API process's memory by ADR 0179: rule questions 4-5 are answered by that ADR, but a POST practice retry replaces the practice and resets anything kept on it (O3a: tutorial `done`/`shown`).
- A background answerer that only logs a failure, without handing the seat back, leaves a question the page is told not to draw: check every failure path ends in a state the page can act on (O3a).
- A hand-back changes the view, not the match: it reaches the stream only through `tutorial.GaveBack += api.Changes.Bump` (ScenarioPlay.cs:69); the #646 test fails if that line goes, so keep it when ScenarioPlay is reworked (#646).
