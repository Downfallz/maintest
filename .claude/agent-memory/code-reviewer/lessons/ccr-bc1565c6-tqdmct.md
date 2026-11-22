# code-reviewer lessons (branch ccr-bc1565c6-tqdmct)

- A `sizes` written as a fixed share (`50vw` "one or two columns") for a grid laid by `repeat(auto-fill, minmax(330px, 1fr))` inside a 1280 px column is wrong once a third column fits (~1054 px and up): compute the column count from the CSS before trusting a `sizes` comment, the journey measure cannot see an over-ask (S8, table/lobby.js CARD_SIZES, c498049).
