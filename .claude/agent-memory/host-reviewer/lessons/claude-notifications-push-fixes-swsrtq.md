# host-reviewer lessons, #1175/#1176/#1151/#1178/#1152 PR

- A new relay word added as a case in WebPubSubRelay.SendAsync is reached by no test: RecordedRelay fakes IChangeRelay and RelayTests touches only the static `Moved`; deleting `case 'n'` falls to `default` ("account") and stays green, while production pages hear the socket, not the stream (#1152 PR, 08378d1).
- The pages key a push test's 502 on the English word `gone` (devices.js:259, battle-services.js:127): a rewording of PushNotices/AccountNotices messages must keep it, and a "gone" for a service the host refuses sends the player round to a 400 on re-subscribe (#1176 PR).
- A page filter on a new stream word (`listsMoved` in lobby.js, `accountMoved` in stats.js) has no page test; only the shared predicate in changes.test.js is pinned (#1152 PR).
