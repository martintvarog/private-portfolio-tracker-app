# Use case: Revalue asset

Step 1 creating exercise 2. Dictated by Martin, written by Claude.
Diagram: `01-manual-assets-use-cases.puml`.

- **Primary actor:** User
- **Goal:** Record a manual asset's new value so net worth is up to date.
- **Preconditions:**
  - The vault is unlocked (the user entered the correct passphrase).
  - The manual asset exists and is not disposed.

## Main success scenario

1. User selects a manual asset to revalue.
2. System asks for the new value and ValuationDate (defaults to today).
3. User enters the new value (in the asset's currency) and confirms the date.
4. System appends a new valuation (value + ValuationDate) to the asset.
5. System performs *Save + snapshot*.
6. System shows the asset with its new value and updated net worth.

## Extensions

- **3a.** ValuationDate is in the future: System rejects it and goes back to step 2.
- **3b.** Value is negative: System shows an error and goes back to step 2.

## Postconditions

- The asset has a new valuation; previous valuations are kept (history).
- A new timestamped vault snapshot exists, and net worth reflects the new value.
