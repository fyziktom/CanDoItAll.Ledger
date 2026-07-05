# QA Prompt

Validate the finished bundle against the raw request, not only the changed code. Check that the BO details Actions tab no longer exposes ownership verification scanning, that Home Security still exposes the ownership verifier, that BO details has an Edit action, and that Edit uses the minting wizard form to stage an update draft for the selected BO id.

Review tests and browser screenshots for semantics, not just presence of labels. A passing result is weak if it only proves a button exists, a dialog opens, or markup is non-empty. The proof must show Home-only verifier placement and update-mode staging. Verify Docker app rebuild transcripts exist and that final raw-note closure rows cite concrete proof paths or commands.
