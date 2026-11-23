# host-reviewer lessons, #1186 PR (series length `of`)

- Since #1186 `RepositoryScenarios.Series` comes from the files' `tutorial.of` (max with orders), not the shipped count; `of` has no upper bound at read time, only `TutorialFileTests.Every_kept_tutorial_names_the_length_of_the_series_and_they_agree` holds it to the file count: a new tutorial must raise `of` in every file (#1186 PR).
- ScenarioFile.Read ignores unknown JSON members, so a field added to a scenario block is safe for an older revision reading a newer file; recheck if UnmappedMemberHandling is ever set (#1186 PR).
- Round 2: a bound on one input of a derived count (`of` <= 20) leaves the other input (`order`, unbounded, used when `of` is absent) able to drive `Enumerable.Range(1, Series)` at start-up; check every input of the value a bound protects (#1186 round 2, bac9177).
