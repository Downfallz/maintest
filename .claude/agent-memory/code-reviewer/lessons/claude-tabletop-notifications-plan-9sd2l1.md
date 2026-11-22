# Lessons from claude/tabletop-notifications-plan-9sd2l1 (N3)

- Second time (N2, then N3): a setter wired only in TableHost, TableWorker or TableTend (`push?.Accounts`,
  `Lobby.Devices`, `Preferences`) is untested when every test assigns it by hand. Ask for a test through the
  composed host, or for the issue that holds it (#1109).
- Moving a send from one sender to another (PushNotices to AccountNotices) moves the dead-endpoint set with it: the
  route's 410 check must read both, or the page never subscribes again (N3, LobbyApi `SubscribeAsync`, df54c19).
