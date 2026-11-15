# Game versions and releases

**Proposed rollout.** Today the showcase and lobby say **Beta**. CI already builds the game image for relevant changes, tags it with the commit and deploys a green main build to Azure (ADR 0182). Keep that fast feedback during beta. A public release marks a build worth announcing to players.

## 1. Start with named beta milestones

- Pick a commit that passed the gate and is running on play.downfallinstinct.com. Verify sign-in, start a match and resume an older one. Give the first milestone an annotated tag such as `v0.1.0-beta.1` and mark its GitHub Release as a prerelease. These numbers are examples.
- Group several PRs into each milestone. Use `v0.1.0-beta.2` for the next iteration of the same milestone. Use `v0.2.0-beta.1` for a new feature milestone. Never move a published tag.
- Write short player-facing notes for features, balance changes and fixes. Identify any effect on matches already in progress or on ranked results. Record the source SHA, deployed image digest and game content hash alongside the notes.
- Between milestones keep showing **Beta**. A showcase-only edit ships independently and does not advance the game version.

## 2. Automate promotion when recurring releases justify it

Add a manually triggered release workflow taking a version and a commit. Validate the version, check the gate on that exact SHA and resolve the existing CI image digest. Serialize releases. Reject a tag already assigned to another commit. A retry for the same version and digest should resume its draft.

Roll out the selected digest to the API, worker and scheduled job using the existing Azure workflow. Check the deployed image and smoke-test the lobby and a resumed match before publishing the draft GitHub Release. Keep the previous successful digest and release manifest. If rollout fails, restore the last compatible digest across the three workloads and leave the draft unpublished. Check persisted match and event compatibility before rollback, especially after a storage migration.

Have the lobby read the deployed release identifier from build or deployment metadata. An untagged beta deployment keeps its generic Beta label. The independently deployed showcase can keep the channel label and link to release notes without fetching a version on page load. The content hash and replay schema continue to identify the rules and saved-event format separately.

## 3. Cut 1.0 when ready

Before the first stable release, switch production promotion from automatic main deployment to explicit release selection. Main can keep producing tested candidates. Promote a release candidate such as `v1.0.0-rc.1`, verify an existing match, ranked results and a rollback rehearsal, then promote that same digest as `v1.0.0`. Remove the beta labels when stable is live.

After 1.0, use patch releases for compatible fixes, minor releases for compatible features and major releases for deliberate compatibility breaks. For an urgent production fix, branch from the live tag, pass the gate, release the next patch and bring the fix back to main.
