# Requirement Traceability

| Requirement | Source input | Owning subbundle | Planned proof | Status |
| --- | --- | --- | --- | --- |
| R001 reusable Send/Merge/Split forms | `bundle://inputs/00-original-request.md` "All those forms..." | SB02 | Source assertions, component tests, browser screenshot | Planned |
| R002 Merge UTXO selector | `bundle://inputs/00-original-request.md` "nice selector of available utxos" | SB02 | Component/browser proof | Planned |
| R003 exclude BO unique/token outputs | `bundle://inputs/00-original-request.md` "except BOs uniquie ones" | SB01, SB02 | Domain rejection tests and source assertions | Planned |
| R004 Merge selected vins -> one vout | `bundle://inputs/00-original-request.md` "takes all selected utxos as vin and vout is just one" | SB01, SB03 | Builder tests and real tx details | Planned |
| R005 Split selected vins | `bundle://inputs/00-original-request.md` "selecte some inputs (even multiple of them)" | SB01, SB02 | Builder/component tests | Planned |
| R006 Split equal amounts | `bundle://inputs/00-original-request.md` "same amounts" | SB01, SB02, SB03 | Tests and real tx details | Planned |
| R007 Split specific amounts | `bundle://inputs/00-original-request.md` "each with specific amount" | SB01, SB02 | Tests and UI proof | Planned |
| R008 reject overspend | `bundle://inputs/00-original-request.md` split output behavior | SB01 | Negative tests | Planned |
| R009 Send still works | `bundle://inputs/00-original-request.md` "all types of those transactions" | SB02, SB03 | Browser Send proof | Planned |
| R010 details layout many rows | `bundle://inputs/00-original-request.md` "plenty of them... overflow/overlay UI" | SB02, SB03 | Large-screen screenshots and review | Planned |
| R011 docker rebuild/restart | `bundle://inputs/00-original-request.md` "rebuild and restart our dockers" | SB03 | Docker transcript | Planned |
| R012 proof in transactions | `bundle://inputs/00-original-request.md` "proof in transactions" | SB03 | Tx IDs and details assertions | Planned |
