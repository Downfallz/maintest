# host-reviewer lessons, #1138/#1139/#980 PR

- A sweep that holds what it cannot let go of and keeps it "outside the gate" does that work inline in whichever request swept, serially, for every account's held practice: a degraded store makes account A's practice route wait on B's, C's writes for the whole grace window; ask for one keeper at a time (an in-flight flag) or work moved after the answer (#1138 PR, ScenarioPractices.KeepSweptAsync).
- A time bound on a retry-holding sweep (`Idle + KeptFor`) is the clause no happy-path test reaches: deleting it stayed green across ScenariosApiTests (measured, af9c72a); ask for a test past the bound with a store that always fails (#1138 PR).
- A catch widened from one call to a whole use case (`OpenAsync` catching StoreFails for the keep) also catches the scenario store's and the content store's reads, and answers them with the keep's code and words (`Tutorial.Unavailable`, "progress could not be saved") (#1138 PR).
- A lookup of the next item by `order + 1` breaks at a gap the store itself admits (`ScenarioStore.Series` counts a dropped tutorial); check gaps whenever a host replaces a page's fallback with a field (#980 PR).
