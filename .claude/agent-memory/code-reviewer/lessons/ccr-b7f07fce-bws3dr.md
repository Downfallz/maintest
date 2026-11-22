# code-reviewer lessons (branch ccr-b7f07fce-bws3dr)

- A page picture moved into a `sceneElement` plate meets identity.css's `.scene-plate > .picture { width: 100% }` (0,2,0), loaded after lobby.css: it beats any page rule on the img at the same specificity (`.sign-in-panel .sign-in-duel`'s width at >= 1280), and `display: contents` on the figure does not stop the child combinator. Measure the img box at desktop when a walk films only 320/390 (scene paintings S9, table/lobby.css:250, 538153e).
