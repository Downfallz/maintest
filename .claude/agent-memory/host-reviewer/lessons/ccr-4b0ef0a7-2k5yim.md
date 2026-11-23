# host-reviewer lessons, notifications N6 PR (the counts)

- A count named after one cause and fed from a worker's catch-all (NoticeWorker's `catch` -> `tally.Refused`) counts every failure of the path as that cause: AccountNotices.SendOneAsync reads and writes the device store, so a storage outage reads as "refusées" in the ops report and the Jobs tab; check what the catch covers before trusting a count's name (N6).
- An admin route added in AdminApi.HostAsync and tested only through its own Api class (NoticesApiTests) has no gate test; the sibling TurnsApi has one in AdminApiTests (The_turn_queues_are_the_operators_alone...): ask for the 403/404 pair through AdminApi for every new HostAsync section (N6).
- A read-modify-write that does housekeeping after its write (NoticeCounts.AddAsync -> ForgetBeforeAsync) turns a housekeeping failure into "could not be kept" for counts already kept, and the housekeeping is never tried again that day: ask for it to be caught on its own (N6).
