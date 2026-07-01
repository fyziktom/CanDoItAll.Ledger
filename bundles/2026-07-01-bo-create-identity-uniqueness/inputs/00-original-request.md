# Original Request

The reported bug:

```text
ect_transition: BusinessObject bo:image:new-image-bo already exists. Create must not overwrite an existing current BusinessObject.
```

The expected behavior:

- Each BO must be unique because of the transaction hash / UTXO lineage.
- BO creation must not reject because content, name, long data, or other metadata are the same.
- If a duplicate appears, analyze whether this is a name-check bug or a transaction assembly bug.
- Use VEFramework Neblio/NFT sources and the linked wiki pages as reference material.
- Prepare and implement as a new CanDoItAll bundle.
