# Fault Catalog

## Planned fault profiles

### `none`
No artificial corruption.

### `truncate-tail`
Truncate the tail by a configured byte count.

### `partial-header`
Simulate a partially written logical header.

### `stale-garbage-tail`
Append non-valid bytes after the last valid logical element.

### `corrupted-declared-count`
Corrupt a count/header field while keeping the rest of the file intact.

### `missing-sidecar-state`
Delete or rename the state sidecar file before reopen.

### `interrupt-before-index-finalize`
Model a stop before index persistence is finalized.

### `interrupt-before-state-save`
Model a stop before state persistence is finalized.
