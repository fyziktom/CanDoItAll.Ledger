# QA Prompt

Validate the bundle as an adversarial reviewer:

- Can a non-participant learn subject, body, file name, description, IPFS CID, URI, attachment size, or content type from raw transaction JSON?
- Does the secure-message object id contain any user-entered text after `bo:secure-message:`?
- Can Alice and Bob still decrypt content and attachments?
- Can legacy v1 messages still open?
- Does raw tx show nested JSON structurally and retain enough information for debugging?
- Do transaction lists remain newest-to-oldest?
- Does Docker proof use rebuilt containers instead of stale local binaries?
