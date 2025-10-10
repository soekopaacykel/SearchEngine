# SearchEngine

Y-scaling (horizontal sharding) support has been added to SearchAPI. The indexer can create multiple SQLite databases where each document resides in exactly one shard. The SearchAPI now performs fan-out queries across configured shards and merges results.

Configuration:
- Configure shard database files in Core\Paths.cs using `SQLITE_SHARD_DATABASES`.
- If at least two valid shard files are present, SearchAPI will use the sharded search; otherwise it falls back to the single database path `SQLITE_DATABASE`.

Architectural options considered:
1) Fan-out in API (implemented): SearchAPI opens N database files and executes the same search per shard, merging sorted results by hit count. Minimal changes, simple deployment.
2) Router/Load balancer service: An intermediate service receives search requests, routes or fans out to shard-specific SearchAPI instances or databases, aggregates responses, and returns to client. More scalable and fault-tolerant but more moving parts. Could be implemented later (e.g., using the existing Loadbalencer project).

Notes:
- Word dictionary lookups (word IDs to names) are assumed consistent across shards. For heterogeneous shards, unioning dictionaries would be required.
- Partial failures are tolerated: if a shard fails, results from other shards are still returned.

Indexer sharding (new):
- The indexer now supports writing to multiple SQLite shard databases. Each document is deterministically routed to exactly one shard (by hashing the file path).
- Configure shard files via Core\Paths.SQLITE_SHARD_DATABASES. If two or more paths are provided, the indexer will initialize all shards and distribute documents across them; otherwise it falls back to a single DB at Core\Paths.SQLITE_DATABASE.
- The words table is written to all shards to keep word IDs consistent across shards during a single indexing run.
