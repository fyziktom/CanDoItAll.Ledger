# Original Request

The user supplied a raw secure-message transaction and identified these concrete problems:

- BO `objectId` contains the message subject: `bo:secure-message:hello-greetings:...`; message subject must be encrypted, not exposed in IDs.
- BO id may keep a type prefix, but the rest should be a hash/random value that does not encode user text.
- Public BO data contains an attachment IPFS hash/URI inside `attachments[].encryptedDataItem`; attachment locators and file metadata must be encrypted.
- If someone finds the IPFS file, the blockchain should not let them connect that file to sender/receiver/message.
- Raw tx display shows nested `stateJson` with escaped `\u0022`; if that is real persisted data, repair or at least make display structured and reduce new secure-message public data.
- This affects more than UI: messaging functions, tests, all BO/transaction display surfaces, and name-based BO ids need review.
- Use `candoitall-bundle-workflow`, prepare detailed architecture and a full bundle, use XLSX to map affected places, then implement and test.

Relevant raw tx excerpt from the user report:

```json
{
  "kind": "BusinessObject",
  "data": {
    "inlineJson": {
      "objectType": "secure-message",
      "stateJson": "{\"format\":\"candoitall-secure-message-v1\", ... \"attachments\":[{\"encryptedDataItem\":{\"hash\":\"QmbYUNCDxFxbTehfS44ptvptgSZdwY3gsgpuWZ9ubmmhh4\",\"uri\":\"http://localhost:5082/ipfs-api/cat?arg=QmbYUNCDxFxbTehfS44ptvptgSZdwY3gsgpuWZ9ubmmhh4\"}}]}"
    },
    "externalRef": "secure-message://bo:secure-message:hello-greetings:8ded917e1d5f5c0-0003-f542d38a3f83"
  },
  "vout": [
    {
      "businessObject": {
        "objectId": "bo:secure-message:hello-greetings:8ded917e1d5f5c0-0003-f542d38a3f83"
      }
    }
  ]
}
```
