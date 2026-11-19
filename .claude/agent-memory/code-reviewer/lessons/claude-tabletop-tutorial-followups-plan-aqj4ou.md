# code-reviewer lessons (branch claude/tabletop-tutorial-followups-plan-aqj4ou)

- A header "fits at width W" measured as the account chip's right edge in the viewport (`toBeInViewport`, `noOverflow`) misses the chip spilling out of the header's own box into the page gutter: the header is 32..vw-32, so its content (1243 px) overflowed from 1280 to 1306 while every check stayed green. Compare `.home-header` scrollWidth with clientWidth, or the chip's right with the header's (T3 #669, d0633e78).
